using OpenPax.Data.Install;
using OpenPax.Data.Png;

namespace OpenPax.Host;

/// <summary>The window's icon: the original executable's, which the importer keeps as data/icon.png.</summary>
internal static class WindowIcon
{
    // The icon is 32x32; taskbars draw larger, so it goes up whole pixels at a time to stay sharp.
    private const int Scale = 4;

    public static void Apply(nint window, string dataFolder)
    {
        var path = Path.Combine(dataFolder, ExecutableIcon.FileName);
        if (!File.Exists(path))
        {
            return;
        }

        var icon = PngReader.Read(File.ReadAllBytes(path));
        var (w, h) = (icon.Width * Scale, icon.Height * Scale);
        var rgba = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var index = icon.Pixels[((y / Scale) * icon.Width) + (x / Scale)];
                var c = icon.Palette[index];
                var at = ((y * w) + x) * 4;
                (rgba[at], rgba[at + 1], rgba[at + 2], rgba[at + 3]) = (c.R, c.G, c.B, index < icon.Alpha.Count ? icon.Alpha[index] : (byte)255);
            }
        }

        Sdl.Sdl.SetWindowIcon(window, w, h, rgba);
    }
}
