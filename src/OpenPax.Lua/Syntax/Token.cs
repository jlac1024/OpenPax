namespace OpenPax.Lua.Syntax;

/// <summary>One token of a script: its kind, its text or value, and its line.</summary>
internal readonly record struct Token(TokenKind Kind, int Line, string Text = "", long Integer = 0, double Float = 0);
