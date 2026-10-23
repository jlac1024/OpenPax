using System.Text;

namespace OpenPax.Host;

/// <summary>Writes to two writers at once (the console, and the log file), never failing for the second.</summary>
internal sealed class TeeWriter(TextWriter first, TextWriter second) : TextWriter
{
    private readonly object _gate = new();

    public override Encoding Encoding => first.Encoding;

    public override void Write(char value)
    {
        lock (_gate)
        {
            first.Write(value);
            Second(w => w.Write(value));
        }
    }

    public override void Write(string? value)
    {
        lock (_gate)
        {
            first.Write(value);
            Second(w => w.Write(value));
        }
    }

    public override void WriteLine(string? value)
    {
        lock (_gate)
        {
            first.WriteLine(value);
            Second(w => w.WriteLine(value));
        }
    }

    public override void Flush()
    {
        lock (_gate)
        {
            first.Flush();
            Second(w => w.Flush());
        }
    }

    private void Second(Action<TextWriter> write)
    {
        try
        {
            write(second);
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            // The log is a help, not a need: the console still has it.
        }
    }
}
