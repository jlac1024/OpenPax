using OpenPax.Data.Png;
using OpenPax.Data.Upscale;

namespace OpenPax.Data.Import;

/// <summary>
/// Remakes every converted picture at twice its size (decisions.md,
/// "Upscaled art and a modern font"): art/set/id.png becomes
/// art2x/set/id.png. The upscaler works in color; its result is put back
/// onto palette indices, each taken from those the source uses at that
/// place (<see cref="ToPalette"/>), so the game still draws it through
/// whatever palette its window uses. Blends that are mostly clear stay clear.
/// </summary>
public static class UpscaleImporter
{
    public const string Folder = "art2x";

    /// <summary>Upscales the art folder; returns how many pictures were made.</summary>
    /// <param name="done">Told as pictures are done: how many, of how many.</param>
    public static int UpscaleAll(string outFolder, Action<int, int>? done = null)
    {
        var art = Path.Combine(outFolder, ArtImporter.ArtFolder);
        var files = Directory.GetFiles(art, "*.png", SearchOption.AllDirectories);
        var made = 0;

        // A quarter of the cores: each picture is small, but memory is limited on the build machine.
        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 4) }, file =>
        {
            var target = Path.Combine(outFolder, Folder, Path.GetRelativePath(art, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); // A file's path always has a folder.
            var png = PngReader.Read(File.ReadAllBytes(file));
            var up = Xbrz2x.Scale(ToRgba(png), png.Width, png.Height);
            using var output = File.Create(target);
            PngWriter.Write(output, new IndexedImage(png.Width * 2, png.Height * 2, ToPalette(up, png), png.Palette, png.Alpha));
            done?.Invoke(Interlocked.Increment(ref made), files.Length);
        });
        return made;
    }

    /// <summary>
    /// Each 2x color as a palette index: under half cover is the
    /// never-drawn index; otherwise the nearest in color of the indices
    /// already used by the source pixel and its eight neighbors. Taking only
    /// indices the picture has there keeps it right under any palette its
    /// window draws it with, not just the one stored with it.
    /// </summary>
    public static byte[] ToPalette(uint[] colors, IndexedImage source)
    {
        const byte Clear = 1;
        var width = source.Width * 2;
        var indices = new byte[colors.Length];
        Span<byte> candidates = stackalloc byte[9];
        for (var i = 0; i < colors.Length; i++)
        {
            var c = colors[i];
            if (c >> 24 < 128)
            {
                indices[i] = Clear;
                continue;
            }

            var (sx, sy) = (i % width / 2, i / width / 2);
            var count = 0;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var x = Math.Clamp(sx + dx, 0, source.Width - 1);
                    var y = Math.Clamp(sy + dy, 0, source.Height - 1);
                    var index = source.Pixels[y * source.Width + x];
                    if (index != Clear && candidates[..count].IndexOf(index) < 0)
                    {
                        candidates[count++] = index;
                    }
                }
            }

            indices[i] = count == 0 ? Clear : Nearest(c, source, candidates[..count]);
        }

        return indices;
    }

    private static byte Nearest(uint rgb, IndexedImage source, ReadOnlySpan<byte> candidates)
    {
        int r = (int)(rgb & 0xFF), g = (int)((rgb >> 8) & 0xFF), b = (int)((rgb >> 16) & 0xFF);
        var best = candidates[0];
        var bestDistance = int.MaxValue;
        foreach (var i in candidates)
        {
            var p = source.Palette[i];
            int dr = p.R - r, dg = p.G - g, db = p.B - b;
            var d = 2 * dr * dr + 4 * dg * dg + 3 * db * db;
            if (d < bestDistance)
            {
                (best, bestDistance) = (i, d);
            }
        }

        return best;
    }

    /// <summary>A converted picture as 0xAABBGGRR colors, through its own palette and transparency.</summary>
    public static uint[] ToRgba(IndexedImage png)
    {
        var pixels = new uint[png.Width * png.Height];
        for (var i = 0; i < pixels.Length; i++)
        {
            var index = png.Pixels[i];
            var c = png.Palette[index];
            var a = index < png.Alpha.Count ? png.Alpha[index] : (byte)255;
            pixels[i] = c.R | ((uint)c.G << 8) | ((uint)c.B << 16) | ((uint)a << 24);
        }

        return pixels;
    }
}
