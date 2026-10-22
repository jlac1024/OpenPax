using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ApiDocs;

/// <summary>
/// The Markdown the docs are written in, as HTML: headings, paragraphs,
/// nested lists, tables, fenced code, inline code, bold and links. Raw HTML
/// lines (an anchor) pass through. Links to other .md pages point at their
/// .html page; links into the source tree, which the site does not hold,
/// become plain code.
/// </summary>
internal static partial class Markdown
{
    public static string ToHtml(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var html = new StringBuilder();
        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            if (line.Trim().Length == 0)
            {
                i++;
            }
            else if (line.StartsWith("```", StringComparison.Ordinal))
            {
                var code = new StringBuilder();
                for (i++; i < lines.Length && !lines[i].StartsWith("```", StringComparison.Ordinal); i++)
                {
                    code.Append(WebUtility.HtmlEncode(lines[i])).Append('\n');
                }

                html.Append("<pre><code>").Append(code).Append("</code></pre>\n");
                i++;
            }
            else if (line.StartsWith('<'))
            {
                html.Append(line).Append('\n');
                i++;
            }
            else if (Heading().Match(line) is { Success: true } h)
            {
                var level = h.Groups[1].Value.Length;
                var text = h.Groups[2].Value;
                html.Append("<h").Append(level).Append(" id=\"").Append(Slug(text)).Append("\">").Append(Inline(text)).Append("</h").Append(level).Append(">\n");
                i++;
            }
            else if (line.StartsWith('|'))
            {
                i = Table(lines, i, html);
            }
            else if (ListItem().IsMatch(line))
            {
                i = List(lines, i, html, Indent(line));
            }
            else
            {
                var paragraph = new List<string>();
                while (i < lines.Length && lines[i].Trim().Length > 0 && !IsBlockStart(lines[i]))
                {
                    paragraph.Add(lines[i].Trim());
                    i++;
                }

                html.Append("<p>").Append(Inline(string.Join(' ', paragraph))).Append("</p>\n");
            }
        }

        return html.ToString();
    }

    /// <summary>A heading's anchor: lower case, letters and digits, dashes between words.</summary>
    public static string Slug(string text) => Slugs().Replace(text.ToLowerInvariant().Replace("`", "", StringComparison.Ordinal), "-").Trim('-');

    private static bool IsBlockStart(string line) =>
        line.StartsWith("```", StringComparison.Ordinal) || line.StartsWith('#') || line.StartsWith('|') || line.StartsWith('<') || ListItem().IsMatch(line);

    private static int Indent(string line) => line.Length - line.TrimStart(' ').Length;

    private static int List(string[] lines, int i, StringBuilder html, int indent)
    {
        var ordered = char.IsDigit(lines[i].TrimStart()[0]);
        html.Append(ordered ? "<ol>\n" : "<ul>\n");
        while (i < lines.Length && ListItem().Match(lines[i]) is { Success: true } m && Indent(lines[i]) == indent)
        {
            var text = new StringBuilder(m.Groups[2].Value);
            i++;

            // Lines indented past the marker go on the item; a deeper marker starts a list inside it.
            var inner = new StringBuilder();
            while (i < lines.Length && lines[i].Trim().Length > 0 && Indent(lines[i]) > indent)
            {
                if (ListItem().IsMatch(lines[i]))
                {
                    i = List(lines, i, inner, Indent(lines[i]));
                }
                else
                {
                    text.Append(' ').Append(lines[i].Trim());
                    i++;
                }
            }

            html.Append("<li>").Append(Inline(text.ToString())).Append(inner).Append("</li>\n");

            // A blank line inside a list keeps the list going when the next item follows.
            if (i + 1 < lines.Length && lines[i].Trim().Length == 0 && ListItem().IsMatch(lines[i + 1]) && Indent(lines[i + 1]) == indent)
            {
                i++;
            }
        }

        html.Append(ordered ? "</ol>\n" : "</ul>\n");
        return i;
    }

    private static int Table(string[] lines, int i, StringBuilder html)
    {
        var rows = new List<string[]>();
        for (; i < lines.Length && lines[i].StartsWith('|'); i++)
        {
            rows.Add(Cells(lines[i]));
        }

        html.Append("<table>\n");
        for (var r = 0; r < rows.Count; r++)
        {
            if (r == 1 && rows[r].All(c => c.Trim().Trim(':').All(ch => ch == '-')))
            {
                continue;
            }

            var tag = r == 0 ? "th" : "td";
            html.Append("<tr>");
            foreach (var cell in rows[r])
            {
                html.Append('<').Append(tag).Append('>').Append(Inline(cell.Trim())).Append("</").Append(tag).Append('>');
            }

            html.Append("</tr>\n");
        }

        html.Append("</table>\n");
        return i;
    }

    /// <summary>A table row's cells; "\|" is a bar inside a cell.</summary>
    private static string[] Cells(string line)
    {
        var body = line.Trim();
        body = body[1..(body.EndsWith('|') ? body.Length - 1 : body.Length)];
        return [.. body.Replace("\\|", "\u0001", StringComparison.Ordinal).Split('|').Select(c => c.Replace('\u0001', '|'))];
    }

    private static string Inline(string text)
    {
        // Code spans first, kept aside so nothing inside them is read as Markdown.
        var spans = new List<string>();
        text = CodeSpan().Replace(text, m =>
        {
            spans.Add("<code>" + WebUtility.HtmlEncode(m.Groups[1].Value) + "</code>");
            return "\u0002" + (spans.Count - 1) + "\u0003";
        });
        text = WebUtility.HtmlEncode(text);
        text = Bold().Replace(text, "<strong>$1</strong>");
        text = Link().Replace(text, m => LinkTo(m.Groups[1].Value, WebUtility.HtmlDecode(m.Groups[2].Value)));
        return Placeholder().Replace(text, m => spans[int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)]);
    }

    private static string LinkTo(string label, string target)
    {
        var hash = target.IndexOf('#', StringComparison.Ordinal);
        var (path, anchor) = hash >= 0 ? (target[..hash], target[hash..]) : (target, "");
        if (path.Length == 0)
        {
            return $"<a href=\"{WebUtility.HtmlEncode(anchor)}\">{label}</a>";
        }

        if (path.StartsWith("http", StringComparison.Ordinal))
        {
            return $"<a href=\"{WebUtility.HtmlEncode(target)}\">{label}</a>";
        }

        if (path.EndsWith(".md", StringComparison.Ordinal))
        {
            return $"<a href=\"{Site.PageFor(path)}{WebUtility.HtmlEncode(anchor)}\">{label}</a>";
        }

        return label;
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^\s*([-*]|\d+\.)\s+(.*)$")]
    private static partial Regex ListItem();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex CodeSpan();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)\)")]
    private static partial Regex Link();

    [GeneratedRegex("\u0002(\\d+)\u0003")]
    private static partial Regex Placeholder();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex Slugs();
}
