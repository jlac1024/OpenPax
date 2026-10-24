using System.Globalization;
using System.Text;
using OpenPax.Lua.Runtime;

namespace OpenPax.Lua.Syntax;

/// <summary>Lua 5.4 source as tokens: names and keywords, numerals, strings (short and long), symbols; comments skipped.</summary>
internal sealed class Lexer(string source, string chunk)
{
    private static readonly Dictionary<string, TokenKind> _keywords = new(StringComparer.Ordinal)
    {
        ["and"] = TokenKind.And,
        ["break"] = TokenKind.Break,
        ["do"] = TokenKind.Do,
        ["else"] = TokenKind.Else,
        ["elseif"] = TokenKind.ElseIf,
        ["end"] = TokenKind.EndKeyword,
        ["false"] = TokenKind.False,
        ["for"] = TokenKind.For,
        ["function"] = TokenKind.Function,
        ["goto"] = TokenKind.Goto,
        ["if"] = TokenKind.If,
        ["in"] = TokenKind.In,
        ["local"] = TokenKind.Local,
        ["nil"] = TokenKind.Nil,
        ["not"] = TokenKind.Not,
        ["or"] = TokenKind.Or,
        ["repeat"] = TokenKind.Repeat,
        ["return"] = TokenKind.Return,
        ["then"] = TokenKind.Then,
        ["true"] = TokenKind.True,
        ["until"] = TokenKind.Until,
        ["while"] = TokenKind.While,
    };

    private int _at;
    private int _line = 1;

    public List<Token> Read()
    {
        var tokens = new List<Token>();
        // A first line starting with # (a shebang) is skipped, as Lua does.
        if (source.StartsWith('#'))
        {
            while (_at < source.Length && source[_at] != '\n')
            {
                _at++;
            }
        }

        while (true)
        {
            SkipSpaceAndComments();
            if (_at >= source.Length)
            {
                tokens.Add(new Token(TokenKind.End, _line));
                return tokens;
            }

            tokens.Add(Next());
        }
    }

    private char Peek(int ahead = 0) => _at + ahead < source.Length ? source[_at + ahead] : '\0';

    private LuaException Error(string message) => new($"{chunk}:{_line}: {message}") { Placed = true };

    private void SkipSpaceAndComments()
    {
        while (_at < source.Length)
        {
            var c = source[_at];
            if (c == '\n')
            {
                _line++;
                _at++;
            }
            else if (c is ' ' or '\t' or '\r' or '\f' or '\v')
            {
                _at++;
            }
            else if (c == '-' && Peek(1) == '-')
            {
                _at += 2;
                if (Peek() == '[' && LongBracketLevel() is { } level)
                {
                    LongString(level);
                }
                else
                {
                    while (_at < source.Length && source[_at] != '\n')
                    {
                        _at++;
                    }
                }
            }
            else
            {
                return;
            }
        }
    }

    private Token Next()
    {
        var c = source[_at];
        var line = _line;
        if (char.IsAsciiLetter(c) || c == '_')
        {
            var start = _at;
            while (_at < source.Length && (char.IsAsciiLetterOrDigit(source[_at]) || source[_at] == '_'))
            {
                _at++;
            }

            var name = source[start.._at];
            return _keywords.TryGetValue(name, out var keyword) ? new Token(keyword, line, name) : new Token(TokenKind.Name, line, name);
        }

        if (char.IsAsciiDigit(c) || (c == '.' && char.IsAsciiDigit(Peek(1))))
        {
            return Numeral();
        }

        if (c is '"' or '\'')
        {
            return new Token(TokenKind.String, line, ShortString(c));
        }

        if (c == '[' && LongBracketLevel() is { } level)
        {
            return new Token(TokenKind.String, line, LongString(level));
        }

        _at++;
        TokenKind Two(char second, TokenKind both, TokenKind one)
        {
            if (Peek() == second)
            {
                _at++;
                return both;
            }

            return one;
        }

        var kind = c switch
        {
            '+' => TokenKind.Plus,
            '-' => TokenKind.Minus,
            '*' => TokenKind.Star,
            '/' => Two('/', TokenKind.DoubleSlash, TokenKind.Slash),
            '%' => TokenKind.Percent,
            '^' => TokenKind.Caret,
            '#' => TokenKind.Hash,
            '&' => TokenKind.Ampersand,
            '~' => Two('=', TokenKind.NotEqual, TokenKind.Tilde),
            '|' => TokenKind.Pipe,
            '<' => Peek() == '<' ? Two('<', TokenKind.ShiftLeft, TokenKind.Less) : Two('=', TokenKind.LessEqual, TokenKind.Less),
            '>' => Peek() == '>' ? Two('>', TokenKind.ShiftRight, TokenKind.Greater) : Two('=', TokenKind.GreaterEqual, TokenKind.Greater),
            '=' => Two('=', TokenKind.Equal, TokenKind.Assign),
            '(' => TokenKind.OpenParen,
            ')' => TokenKind.CloseParen,
            '{' => TokenKind.OpenBrace,
            '}' => TokenKind.CloseBrace,
            '[' => TokenKind.OpenBracket,
            ']' => TokenKind.CloseBracket,
            ';' => TokenKind.Semicolon,
            ':' => Two(':', TokenKind.DoubleColon, TokenKind.Colon),
            ',' => TokenKind.Comma,
            '.' => Peek() == '.' ? (Peek(1) == '.' ? Three() : Two('.', TokenKind.Concat, TokenKind.Dot)) : TokenKind.Dot,
            _ => throw Error($"unexpected symbol near '{c}'"),
        };
        return new Token(kind, line);

        TokenKind Three()
        {
            _at += 2;
            return TokenKind.Ellipsis;
        }
    }

    private Token Numeral()
    {
        var line = _line;
        var start = _at;
        var hex = Peek() == '0' && Peek(1) is 'x' or 'X';
        if (hex)
        {
            _at += 2;
        }

        while (_at < source.Length)
        {
            var c = source[_at];
            var exponent = hex ? c is 'p' or 'P' : c is 'e' or 'E';
            if (exponent && Peek(1) is '+' or '-')
            {
                _at += 2;
            }
            else if (char.IsAsciiLetterOrDigit(c) || c == '.')
            {
                _at++;
            }
            else
            {
                break;
            }
        }

        var text = source[start.._at];
        if (hex && text.AsSpan(2).IndexOfAny(".pP") >= 0)
        {
            throw Error($"hexadecimal floats are not supported near '{text}'");
        }

        var value = Numbers.Parse(text);
        if (value.IsInteger)
        {
            return new Token(TokenKind.Integer, line, text, Integer: value.AsInteger);
        }

        if (value.IsFloat)
        {
            return new Token(TokenKind.Float, line, text, Float: value.AsFloat);
        }

        throw Error($"malformed number near '{text}'");
    }

    private string ShortString(char quote)
    {
        _at++;
        var text = new StringBuilder();
        while (true)
        {
            if (_at >= source.Length || source[_at] == '\n')
            {
                throw Error("unfinished string");
            }

            var c = source[_at++];
            if (c == quote)
            {
                return text.ToString();
            }

            if (c != '\\')
            {
                text.Append(c);
                continue;
            }

            var e = Peek();
            _at++;
            switch (e)
            {
                case 'n': text.Append('\n'); break;
                case 't': text.Append('\t'); break;
                case 'r': text.Append('\r'); break;
                case 'a': text.Append('\a'); break;
                case 'b': text.Append('\b'); break;
                case 'f': text.Append('\f'); break;
                case 'v': text.Append('\v'); break;
                case '\\': text.Append('\\'); break;
                case '"': text.Append('"'); break;
                case '\'': text.Append('\''); break;
                case '\n':
                    _line++;
                    text.Append('\n');
                    break;
                case 'x':
                    if (!char.IsAsciiHexDigit(Peek()) || !char.IsAsciiHexDigit(Peek(1)))
                    {
                        throw Error("hexadecimal digit expected");
                    }

                    text.Append((char)int.Parse(source.AsSpan(_at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    _at += 2;
                    break;
                case 'z':
                    while (_at < source.Length && char.IsWhiteSpace(source[_at]))
                    {
                        if (source[_at] == '\n')
                        {
                            _line++;
                        }

                        _at++;
                    }

                    break;
                case 'u':
                    {
                        if (Peek() != '{')
                        {
                            throw Error("missing '{' in \\u{xxxx}");
                        }

                        var close = source.IndexOf('}', _at);
                        if (close < 0 || !int.TryParse(source.AsSpan(_at + 1, close - _at - 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) || code > 0x10FFFF)
                        {
                            throw Error("UTF-8 value too large or malformed");
                        }

                        text.Append(char.ConvertFromUtf32(code));
                        _at = close + 1;
                        break;
                    }

                default:
                    if (char.IsAsciiDigit(e))
                    {
                        var digits = 1;
                        while (digits < 3 && char.IsAsciiDigit(Peek()))
                        {
                            _at++;
                            digits++;
                        }

                        var code = int.Parse(source.AsSpan(_at - digits, digits), CultureInfo.InvariantCulture);
                        if (code > 255)
                        {
                            throw Error("decimal escape too large");
                        }

                        text.Append((char)code);
                        break;
                    }

                    throw Error($"invalid escape sequence '\\{e}'");
            }
        }
    }

    // [==[ : the number of = signs, or null when this '[' does not open a long bracket.
    private int? LongBracketLevel()
    {
        var level = 0;
        while (Peek(1 + level) == '=')
        {
            level++;
        }

        return Peek(1 + level) == '[' ? level : null;
    }

    private string LongString(int level)
    {
        _at += level + 2;
        // A newline straight after the opening bracket is not part of the string.
        if (Peek() == '\r')
        {
            _at++;
        }

        if (Peek() == '\n')
        {
            _line++;
            _at++;
        }

        var close = "]" + new string('=', level) + "]";
        var end = source.IndexOf(close, _at, StringComparison.Ordinal);
        if (end < 0)
        {
            throw Error("unfinished long string or comment");
        }

        var text = source[_at..end];
        _line += text.Count(c => c == '\n');
        _at = end + close.Length;
        return text;
    }
}
