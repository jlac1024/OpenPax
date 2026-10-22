using System.Globalization;
using System.Text;

namespace OpenPax.Data.Toml;

/// <summary>
/// Reads the TOML subset the port writes and reads: comments, [tables] and
/// [[arrays of tables]] with dotted names, and key = value with integers,
/// floats, booleans, basic strings and arrays of those (on one line or
/// several). Tables are
/// Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, integers long.
/// </summary>
public static class TomlReader
{
    public static Dictionary<string, object> Parse(string text)
    {
        var root = new Dictionary<string, object>();
        var current = root;
        var lineNumber = 0;
        var lines = text.Split('\n');
        for (var n = 0; n < lines.Length; n++)
        {
            lineNumber = n + 1;
            var line = StripComment(lines[n]).Trim();

            // An array may run over several lines: the lines are joined until its brackets close.
            if (line.Contains('=', StringComparison.Ordinal) && !line.StartsWith('['))
            {
                while (Depth(line[(line.IndexOf('=') + 1)..]) > 0 && n + 1 < lines.Length)
                {
                    line += " " + StripComment(lines[++n]).Trim();
                }
            }

            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("[[", StringComparison.Ordinal))
            {
                var path = line[2..line.IndexOf("]]", StringComparison.Ordinal)].Trim().Split('.');
                var parent = Walk(root, path[..^1], lineNumber);
                if (!parent.TryGetValue(path[^1], out var existing))
                {
                    existing = new List<object>();
                    parent[path[^1]] = existing;
                }

                var table = new Dictionary<string, object>();
                ((List<object>)existing).Add(table);
                current = table;
            }
            else if (line.StartsWith('['))
            {
                current = Walk(root, line[1..line.IndexOf(']')].Trim().Split('.'), lineNumber);
            }
            else
            {
                var eq = line.IndexOf('=');
                if (eq < 0)
                {
                    throw Error(lineNumber);
                }

                var key = line[..eq].Trim();
                var pos = 0;
                var value = line[(eq + 1)..].Trim();
                current[key] = ParseValue(value, ref pos, lineNumber);
            }
        }

        return root;
    }

    /// <summary>How many array brackets are open at the end of a value, outside strings.</summary>
    private static int Depth(string value)
    {
        var (depth, inString) = (0, false);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '"' && (i == 0 || value[i - 1] != '\\'))
            {
                inString = !inString;
            }
            else if (!inString && value[i] == '[')
            {
                depth++;
            }
            else if (!inString && value[i] == ']')
            {
                depth--;
            }
        }

        return depth;
    }

    /// <summary>Reads an integer field, failing with a plain message when it is missing.</summary>
    public static int Int(Dictionary<string, object> table, string key)
    {
        return table.TryGetValue(key, out var v) && v is long l ? checked((int)l) : throw new FormatException(string.Format(Strings.TomlMissing, key));
    }

    public static string String(Dictionary<string, object> table, string key)
    {
        return table.TryGetValue(key, out var v) && v is string s ? s : throw new FormatException(string.Format(Strings.TomlMissing, key));
    }

    public static IEnumerable<Dictionary<string, object>> Tables(Dictionary<string, object> table, string key)
    {
        return table.TryGetValue(key, out var v) && v is List<object> list ? list.Cast<Dictionary<string, object>>() : [];
    }

    private static Dictionary<string, object> Walk(Dictionary<string, object> root, string[] path, int line)
    {
        var t = root;
        foreach (var part in path)
        {
            if (!t.TryGetValue(part, out var next))
            {
                next = new Dictionary<string, object>();
                t[part] = next;
            }

            t = next switch
            {
                Dictionary<string, object> d => d,
                List<object> l when l.Count > 0 && l[^1] is Dictionary<string, object> last => last,
                _ => throw Error(line),
            };
        }

        return t;
    }

    private static object ParseValue(string s, ref int pos, int line)
    {
        SkipSpace(s, ref pos);
        if (pos >= s.Length)
        {
            throw Error(line);
        }

        if (s[pos] == '"')
        {
            return ParseString(s, ref pos, line);
        }

        if (s[pos] == '[')
        {
            pos++;
            var list = new List<object>();
            while (true)
            {
                SkipSpace(s, ref pos);
                if (pos < s.Length && s[pos] == ']')
                {
                    pos++;
                    return list;
                }

                list.Add(ParseValue(s, ref pos, line));
                SkipSpace(s, ref pos);
                if (pos < s.Length && s[pos] == ',')
                {
                    pos++;
                }
            }
        }

        var start = pos;
        while (pos < s.Length && s[pos] != ',' && s[pos] != ']' && !char.IsWhiteSpace(s[pos]))
        {
            pos++;
        }

        var token = s[start..pos];
        if (token == "true")
        {
            return true;
        }

        if (token == "false")
        {
            return false;
        }

        if (long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l))
        {
            return l;
        }

        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }

        throw Error(line);
    }

    private static string ParseString(string s, ref int pos, int line)
    {
        pos++;
        var sb = new StringBuilder();
        while (pos < s.Length && s[pos] != '"')
        {
            if (s[pos] == '\\' && pos + 1 < s.Length)
            {
                pos++;
                sb.Append(s[pos] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    _ => throw Error(line),
                });
            }
            else
            {
                sb.Append(s[pos]);
            }

            pos++;
        }

        if (pos >= s.Length)
        {
            throw Error(line);
        }

        pos++;
        return sb.ToString();
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inString = !inString;
            }
            else if (line[i] == '#' && !inString)
            {
                return line[..i];
            }
        }

        return line;
    }

    private static void SkipSpace(string s, ref int pos)
    {
        while (pos < s.Length && char.IsWhiteSpace(s[pos]))
        {
            pos++;
        }
    }

    private static FormatException Error(int line)
    {
        return new FormatException(string.Format(Strings.TomlBadLine, line));
    }
}
