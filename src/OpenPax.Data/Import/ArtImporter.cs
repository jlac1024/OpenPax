using System.Globalization;
using System.Text;
using OpenPax.Data.Art;
using OpenPax.Data.Png;
using OpenPax.Data.Resources;

namespace OpenPax.Data.Import;

/// <summary>
/// Converts the original art to modern files: every <c>PPIC</c> to an
/// 8-bit indexed PNG, every <c>CPLT</c> to a JASC palette. See
/// docs/formats/converted.md for the output layout.
/// </summary>
public static class ArtImporter
{
    public const string ArtFolder = "art";
    public const string PaletteFolder = "palettes";
    public const string PictureIndex = "pictures.toml";

    /// <summary>Counts from converting one image file.</summary>
    public sealed record ImageFileResult(int Pictures, int UncertainPalettes);

    public static IReadOnlyList<Palette> ImportPalettes(string gameFolder, string outFolder)
    {
        var file = ResourceFile.Load(Path.Combine(gameFolder, "Data", "PalData"));
        var dir = Directory.CreateDirectory(Path.Combine(outFolder, PaletteFolder)).FullName;
        var palettes = new List<Palette>();
        foreach (var entry in file.Entries(ResourceType.Palette))
        {
            var palette = Palette.Parse(file.Get(ResourceType.Palette, entry.Id).Span);
            palettes.Add(palette);
            File.WriteAllText(Path.Combine(dir, $"{entry.Id}.pal"), JascPalette.Format(palette));
        }

        return palettes;
    }

    /// <summary>Converts one <c>Image/*.img</c> file.</summary>
    public static ImageFileResult ImportImageFile(string imgPath, string outFolder, IReadOnlyList<Palette> palettes)
    {
        var file = ResourceFile.Load(imgPath);
        var stem = Path.GetFileNameWithoutExtension(imgPath).ToLowerInvariant();
        var dir = Directory.CreateDirectory(Path.Combine(outFolder, ArtFolder, stem)).FullName;

        var index = new StringBuilder();
        index.Append(CultureInfo.InvariantCulture, $"# Pictures converted from Image/{Path.GetFileName(imgPath)}.\n");
        index.Append(CultureInfo.InvariantCulture, $"# palette: the Data/PalData palette embedded in the PNG, measured per group of {PaletteGuess.GroupSize} ids.\n");
        index.Append("# At runtime pictures are drawn with their window's palette; see docs/formats/converted.md.\n\n");

        var entries = file.Entries(ResourceType.Picture);

        // First pass: measure every picture, then pool the measurements per id group.
        var measurements = new PaletteGuess.Measurement[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            measurements[i] = PaletteGuess.Measure(Picture.Parse(file.GetByIndex(ResourceType.Picture, i).Span), palettes);
        }

        var groups = Enumerable.Range(0, entries.Count)
            .GroupBy(i => entries[i].Id / PaletteGuess.GroupSize)
            .ToDictionary(g => g.Key, g => PaletteGuess.ChooseForGroup([.. g.Select(i => measurements[i])], palettes.Count));

        int? previousId = null;
        var repeat = 0;
        var uncertain = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            repeat = entry.Id == previousId ? repeat + 1 : 0;
            previousId = entry.Id;
            var picture = Picture.Parse(file.GetByIndex(ResourceType.Picture, i).Span);
            var group = groups[entry.Id / PaletteGuess.GroupSize];
            var chosen = PaletteGuess.ChooseForPicture(measurements[i], group);
            var isUncertain = PaletteGuess.IsUncertain(picture, chosen, group, palettes);
            if (isUncertain)
            {
                uncertain++;
            }

            // The original looks pictures up by id and always gets the first
            // of a repeated id, so only that one keeps the plain name.
            var name = repeat == 0 ? $"{entry.Id}.png" : $"{entry.Id}.{repeat}.png";
            using (var png = File.Create(Path.Combine(dir, name)))
            {
                PngWriter.Write(png, ToIndexedImage(picture, palettes[chosen]));
            }

            index.Append("[[picture]]\n");
            index.Append(CultureInfo.InvariantCulture, $"id = {entry.Id}\n");
            index.Append(CultureInfo.InvariantCulture, $"file = \"{name}\"\n");
            if (repeat > 0)
            {
                index.Append("unreachable_by_id = true\n");
            }

            index.Append(CultureInfo.InvariantCulture, $"left = {picture.Left}\ntop = {picture.Top}\n");
            index.Append(CultureInfo.InvariantCulture, $"width = {picture.Width}\nheight = {picture.Height}\n");
            index.Append(CultureInfo.InvariantCulture, $"palette = {1000 + chosen}\n");
            if (chosen != group.Palette)
            {
                index.Append(CultureInfo.InvariantCulture, $"palette_differs_from_group = {1000 + group.Palette}\n");
            }

            if (isUncertain)
            {
                index.Append(CultureInfo.InvariantCulture, $"palette_uncertain = true\npalette_runner_up = {1000 + group.RunnerUp}\n");
            }

            index.Append('\n');
        }

        File.WriteAllText(Path.Combine(dir, PictureIndex), index.ToString());
        return new ImageFileResult(entries.Count, uncertain);
    }

    public static IndexedImage ToIndexedImage(Picture picture, Palette palette)
    {
        // Only index 1 is see-through, as in the original's blitters.
        return new IndexedImage(picture.Width, picture.Height, picture.Pixels, palette.Colors, [255, 0]);
    }
}
