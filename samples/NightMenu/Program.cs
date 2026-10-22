using OpenPax.Data.Art;
using OpenPax.Data.Png;

namespace NightMenu;

/// <summary>Makes the Night Menu data pack from the imported game data: NightMenu &lt;data folder&gt; &lt;pack folder&gt;.</summary>
internal static class Program
{
    // The Game Start screen's picture (VIEW 31000), at 1x and 2x.
    private static readonly string[] _pictures = [Path.Combine("art", "main", "31000.png"), Path.Combine("art2x", "main", "31000.png")];

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: NightMenu <data folder> <pack folder>");
            return 2;
        }

        var (data, pack) = (args[0], args[1]);
        foreach (var picture in _pictures)
        {
            var from = Path.Combine(data, picture);
            if (!File.Exists(from))
            {
                Console.Error.WriteLine($"{from} not found; the pack is left without it.");
                continue;
            }

            var to = Path.Combine(pack, "data", picture);
            Directory.CreateDirectory(Path.GetDirectoryName(to) ?? pack);
            using var output = File.Create(to);
            PngWriter.Write(output, Night(PngReader.Read(File.ReadAllBytes(from))));
        }

        File.Copy(Path.Combine(AppContext.BaseDirectory, "mod.toml"), Path.Combine(pack, "mod.toml"), true);
        return 0;
    }

    /// <summary>
    /// The picture at night. Pictures are drawn with the screen's palette, not
    /// their own, so only the indices may change: each color is moved to the
    /// palette entry nearest a darker, bluer version of it.
    /// </summary>
    private static IndexedImage Night(IndexedImage image)
    {
        var palette = image.Palette;
        var map = new byte[256];
        for (var i = 0; i < map.Length; i++)
        {
            map[i] = (byte)i;
        }

        for (var i = 0; i < palette.Count; i++)
        {
            // An index that is see-through stays as it is.
            if (i < image.Alpha.Count && image.Alpha[i] < 255)
            {
                continue;
            }

            var c = palette[i];
            var gray = ((c.R * 30) + (c.G * 59) + (c.B * 11)) / 100;
            var want = new Rgb((byte)(gray * 0.25), (byte)(gray * 0.35), (byte)Math.Min(255, (gray * 0.7) + 12));
            map[i] = Nearest(palette, image.Alpha, want);
        }

        var pixels = image.Pixels.Select(p => map[p]).ToArray();
        return image with { Pixels = pixels };
    }

    private static byte Nearest(IReadOnlyList<Rgb> palette, IReadOnlyList<byte> alpha, Rgb want)
    {
        var best = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < palette.Count; i++)
        {
            if (i < alpha.Count && alpha[i] < 255)
            {
                continue;
            }

            var (dr, dg, db) = (palette[i].R - want.R, palette[i].G - want.G, palette[i].B - want.B);
            var distance = (dr * dr * 3) + (dg * dg * 4) + (db * db * 2);
            if (distance < bestDistance)
            {
                (best, bestDistance) = (i, distance);
            }
        }

        return (byte)best;
    }
}
