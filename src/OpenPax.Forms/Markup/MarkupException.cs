namespace OpenPax.Forms.Markup;

/// <summary>Markup that cannot be read, with the line it went wrong on.</summary>
public sealed class MarkupException(string message, int line) : Exception(line > 0 ? $"{message} (line {line})" : message)
{
    public int Line { get; } = line;
}
