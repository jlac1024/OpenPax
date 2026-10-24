using OpenPax.Lua.Runtime;

namespace OpenPax.Lua.Library;

/// <summary>
/// Lua's patterns (not regular expressions): classes like %a and %d, sets
/// [...], the quantifiers * + - ?, anchors ^ and $, captures (including the
/// position capture ()), %b balanced pairs, %f frontiers and back references
/// %1 to %9. Matching backtracks as Lua's does, with a cap on the work so a
/// pathological pattern cannot hang the game.
/// </summary>
internal sealed class LuaPattern
{
    private const int MaxCaptures = 32;
    private const int PositionCapture = -2;
    private const int Unclosed = -1;

    private readonly string _text;
    private readonly string _pattern;
    private readonly int[] _captureStart = new int[MaxCaptures];
    private readonly int[] _captureLength = new int[MaxCaptures];
    private int _captures;
    private int _depth;
    private long _work;

    private LuaPattern(string text, string pattern)
    {
        _text = text;
        _pattern = pattern;
    }

    /// <summary>The first match at or after start: where it starts and ends (exclusive), and its captures; null when none.</summary>
    public static (int Start, int End, LuaValue[] Captures)? Find(string text, string pattern, int start, bool wholeIfNone)
    {
        var anchored = pattern.StartsWith('^');
        var p = anchored ? 1 : 0;
        for (var s = start; s <= text.Length; s++)
        {
            var m = new LuaPattern(text, pattern);
            if (m.Match(s, p) is { } end)
            {
                return (s, end, m.Captures(s, end, wholeIfNone));
            }

            if (anchored)
            {
                break;
            }
        }

        return null;
    }

    /// <summary>Every match from start on, as gmatch and gsub step through them.</summary>
    public static IEnumerable<(int Start, int End, LuaValue[] Captures)> All(string text, string pattern)
    {
        var anchored = pattern.StartsWith('^');
        var p = anchored ? 1 : 0;
        var s = 0;
        var lastEnd = -1;
        while (s <= text.Length)
        {
            var m = new LuaPattern(text, pattern);
            if (m.Match(s, p) is { } end && end != lastEnd)
            {
                yield return (s, end, m.Captures(s, end, wholeIfNone: true));
                lastEnd = end;
                s = end > s ? end : s + 1;
            }
            else
            {
                s++;
            }

            if (anchored)
            {
                break;
            }
        }
    }

    private LuaValue[] Captures(int start, int end, bool wholeIfNone)
    {
        if (_captures == 0 && wholeIfNone)
        {
            return [LuaValue.Of(_text[start..end])];
        }

        var values = new LuaValue[_captures];
        for (var i = 0; i < _captures; i++)
        {
            values[i] = Capture(i);
        }

        return values;
    }

    private LuaValue Capture(int i)
    {
        if (_captureLength[i] == Unclosed)
        {
            throw new LuaException("unfinished capture");
        }

        return _captureLength[i] == PositionCapture
            ? LuaValue.Of(_captureStart[i] + 1L)
            : LuaValue.Of(_text.Substring(_captureStart[i], _captureLength[i]));
    }

    private LuaException Malformed(string why) => new($"malformed pattern ({why})");

    // Where the text matches the pattern from s and p on to the pattern's end: the match's end, or null.
    private int? Match(int s, int p)
    {
        if (++_work > 1_000_000 || ++_depth > 200)
        {
            throw new LuaException("pattern too complex");
        }

        try
        {
            while (true)
            {
                if (p == _pattern.Length)
                {
                    return s;
                }

                switch (_pattern[p])
                {
                    case '(':
                        return p + 1 < _pattern.Length && _pattern[p + 1] == ')'
                            ? StartCapture(s, p + 2, PositionCapture)
                            : StartCapture(s, p + 1, Unclosed);
                    case ')':
                        return EndCapture(s, p + 1);
                    case '$' when p + 1 == _pattern.Length:
                        return s == _text.Length ? s : null;
                    case '%' when p + 1 < _pattern.Length && _pattern[p + 1] == 'b':
                        {
                            if (Balance(s, p + 2) is not { } after)
                            {
                                return null;
                            }

                            (s, p) = (after, p + 4);
                            continue;
                        }

                    case '%' when p + 1 < _pattern.Length && _pattern[p + 1] == 'f':
                        {
                            p += 2;
                            if (p >= _pattern.Length || _pattern[p] != '[')
                            {
                                throw Malformed("missing '[' after '%f'");
                            }

                            var end = ClassEnd(p);
                            var before = s == 0 ? '\0' : _text[s - 1];
                            var here = s < _text.Length ? _text[s] : '\0';
                            if (!MatchSet(before, p, end - 1) && MatchSet(here, p, end - 1))
                            {
                                p = end;
                                continue;
                            }

                            return null;
                        }

                    case '%' when p + 1 < _pattern.Length && char.IsAsciiDigit(_pattern[p + 1]):
                        {
                            var index = _pattern[p + 1] - '1';
                            if (index < 0 || index >= _captures || _captureLength[index] == Unclosed)
                            {
                                throw new LuaException($"invalid capture index %{index + 1}");
                            }

                            var length = _captureLength[index];
                            if (_text.Length - s >= length && string.CompareOrdinal(_text, _captureStart[index], _text, s, length) == 0)
                            {
                                (s, p) = (s + length, p + 2);
                                continue;
                            }

                            return null;
                        }
                }

                var classEnd = ClassEnd(p);
                var matches = s < _text.Length && SingleMatch(_text[s], p, classEnd);
                var quantifier = classEnd < _pattern.Length ? _pattern[classEnd] : '\0';
                switch (quantifier)
                {
                    case '?':
                        if (matches && Match(s + 1, classEnd + 1) is { } taken)
                        {
                            return taken;
                        }

                        p = classEnd + 1;
                        continue;
                    case '+':
                        return matches ? MaxExpand(s + 1, p, classEnd) : null;
                    case '*':
                        return MaxExpand(s, p, classEnd);
                    case '-':
                        return MinExpand(s, p, classEnd);
                    default:
                        if (!matches)
                        {
                            return null;
                        }

                        (s, p) = (s + 1, classEnd);
                        continue;
                }
            }
        }
        finally
        {
            _depth--;
        }
    }

    private int? StartCapture(int s, int p, int what)
    {
        if (_captures >= MaxCaptures)
        {
            throw new LuaException("too many captures");
        }

        _captureStart[_captures] = s;
        _captureLength[_captures] = what;
        _captures++;
        var end = Match(s, p);
        if (end is null)
        {
            _captures--;
        }

        return end;
    }

    private int? EndCapture(int s, int p)
    {
        var open = -1;
        for (var i = _captures - 1; i >= 0; i--)
        {
            if (_captureLength[i] == Unclosed)
            {
                open = i;
                break;
            }
        }

        if (open < 0)
        {
            throw Malformed("invalid pattern capture");
        }

        _captureLength[open] = s - _captureStart[open];
        var end = Match(s, p);
        if (end is null)
        {
            _captureLength[open] = Unclosed;
        }

        return end;
    }

    private int? MaxExpand(int s, int p, int classEnd)
    {
        var count = 0;
        while (s + count < _text.Length && SingleMatch(_text[s + count], p, classEnd))
        {
            count++;
        }

        for (; count >= 0; count--)
        {
            if (Match(s + count, classEnd + 1) is { } end)
            {
                return end;
            }
        }

        return null;
    }

    private int? MinExpand(int s, int p, int classEnd)
    {
        while (true)
        {
            if (Match(s, classEnd + 1) is { } end)
            {
                return end;
            }

            if (s < _text.Length && SingleMatch(_text[s], p, classEnd))
            {
                s++;
            }
            else
            {
                return null;
            }
        }
    }

    private int? Balance(int s, int p)
    {
        if (p + 1 >= _pattern.Length)
        {
            throw Malformed("missing arguments to '%b'");
        }

        if (s >= _text.Length || _text[s] != _pattern[p])
        {
            return null;
        }

        var (open, close) = (_pattern[p], _pattern[p + 1]);
        var depth = 1;
        for (var i = s + 1; i < _text.Length; i++)
        {
            if (_text[i] == close)
            {
                if (--depth == 0)
                {
                    return i + 1;
                }
            }
            else if (_text[i] == open)
            {
                depth++;
            }
        }

        return null;
    }

    // Where the single character class at p ends: after %x, after a set's ], or one on.
    private int ClassEnd(int p)
    {
        var c = _pattern[p++];
        if (c == '%')
        {
            if (p >= _pattern.Length)
            {
                throw Malformed("ends with '%'");
            }

            return p + 1;
        }

        if (c == '[')
        {
            if (p < _pattern.Length && _pattern[p] == '^')
            {
                p++;
            }

            do
            {
                if (p >= _pattern.Length)
                {
                    throw Malformed("missing ']'");
                }

                c = _pattern[p++];
                if (c == '%')
                {
                    p++;
                }
            }
            while (p >= _pattern.Length || _pattern[p] != ']');

            return p + 1;
        }

        return p;
    }

    private bool SingleMatch(char c, int p, int end) => _pattern[p] switch
    {
        '.' => true,
        '%' => ClassMatch(c, _pattern[p + 1]),
        '[' => MatchSet(c, p, end - 1),
        _ => _pattern[p] == c,
    };

    // A set from its [ at p to its ] at end.
    private bool MatchSet(char c, int p, int end)
    {
        var negate = false;
        p++;
        if (_pattern[p] == '^')
        {
            negate = true;
            p++;
        }

        while (p < end)
        {
            if (_pattern[p] == '%' && p + 1 < end)
            {
                p++;
                if (ClassMatch(c, _pattern[p]))
                {
                    return !negate;
                }

                p++;
            }
            else if (p + 2 < end && _pattern[p + 1] == '-')
            {
                if (_pattern[p] <= c && c <= _pattern[p + 2])
                {
                    return !negate;
                }

                p += 3;
            }
            else
            {
                if (_pattern[p] == c)
                {
                    return !negate;
                }

                p++;
            }
        }

        return negate;
    }

    // %a, %d and the rest, in the C locale (ASCII), as Lua's are; the capital letter is the complement.
    private static bool ClassMatch(char c, char cl)
    {
        var result = char.ToLowerInvariant(cl) switch
        {
            'a' => char.IsAsciiLetter(c),
            'c' => c < 32 || c == 127,
            'd' => char.IsAsciiDigit(c),
            'g' => c > 32 && c < 127,
            'l' => char.IsAsciiLetterLower(c),
            'p' => c > 32 && c < 127 && !char.IsAsciiLetterOrDigit(c),
            's' => c is ' ' or '\t' or '\n' or '\r' or '\f' or '\v',
            'u' => char.IsAsciiLetterUpper(c),
            'w' => char.IsAsciiLetterOrDigit(c),
            'x' => char.IsAsciiHexDigit(c),
            _ => (bool?)null,
        };
        if (result is null)
        {
            return cl == c;
        }

        return char.IsUpper(cl) ? !result.Value : result.Value;
    }
}
