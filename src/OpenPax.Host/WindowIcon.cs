namespace OpenPax.Host;

/// <summary>
/// The window's icon: OpenPax's own (Icon/openpax.svg), which the program
/// carries as 128x128 raw pixels (tools/make_icon.sh), so the taskbar shows
/// the same icon as the shortcuts and the installer, before any import.
/// </summary>
internal static class WindowIcon
{
    private const int Size = 128;
    private const string Resource = "openpax-128.rgba";

    public static void Apply(nint window)
    {
        using var stream = typeof(WindowIcon).Assembly.GetManifestResourceStream(Resource);
        if (stream is null || stream.Length != Size * Size * 4)
        {
            return;
        }

        var rgba = new byte[Size * Size * 4];
        stream.ReadExactly(rgba);
        Sdl.Sdl.SetWindowIcon(window, Size, Size, rgba);
    }
}
