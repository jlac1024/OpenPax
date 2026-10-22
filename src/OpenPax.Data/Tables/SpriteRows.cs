using System.Globalization;
using System.Text;
using OpenPax.Data.Art;
using OpenPax.Data.Resources;
using OpenPax.Data.Toml;

namespace OpenPax.Data.Tables;

/// <summary>
/// For each picture of an image file, the index of its last row holding a
/// pixel that is not transparent, and its size. Combat uses
/// the row for the footprint of orbital defenses and the size to place a
/// planet's blocks, so the simulation needs them without art.
/// </summary>
public static class SpriteRows
{
    public const string FileName = "combat-sprites.toml";

    /// <summary>Measures every picture; a repeated id keeps the first, as lookup by id does.</summary>
    public static CombatSprites Measure(ResourceFile imageFile)
    {
        var rows = new Dictionary<int, int>();
        var sizes = new Dictionary<int, (int Width, int Height)>();
        foreach (var entry in imageFile.Entries(ResourceType.Picture))
        {
            if (rows.ContainsKey(entry.Id))
            {
                continue;
            }

            var picture = Picture.Parse(imageFile.Get(ResourceType.Picture, entry.Id).Span);
            rows[entry.Id] = LastRow(picture);
            sizes[entry.Id] = (picture.Width, picture.Height);
        }

        return new CombatSprites(rows, sizes);
    }

    /// <summary>The last row, from the bottom up to row 1, with a pixel that is not index 1; 0 if none.</summary>
    private static int LastRow(Picture picture)
    {
        for (var row = picture.Height - 1; row > 0; row--)
        {
            for (var col = 0; col < picture.Width; col++)
            {
                if (picture.Pixels[row * picture.Width + col] != Picture.Transparent)
                {
                    return row;
                }
            }
        }

        return 0;
    }

    public static string ToToml(CombatSprites sprites)
    {
        var text = new StringBuilder("# Each Image/Combat.img picture: r last row with a visible pixel, w width, h height.\n");
        foreach (var (id, row) in sprites.Rows.OrderBy(r => r.Key))
        {
            var (w, h) = sprites.Sizes[id];
            text.Append(CultureInfo.InvariantCulture, $"r{id} = {row}\nw{id} = {w}\nh{id} = {h}\n");
        }

        return text.ToString();
    }

    public static CombatSprites FromToml(string text)
    {
        var values = TomlReader.Parse(text).ToDictionary(kv => kv.Key, kv => checked((int)(long)kv.Value));
        Dictionary<int, int> Column(char prefix) => values.Where(kv => kv.Key[0] == prefix)
            .ToDictionary(kv => int.Parse(kv.Key[1..], CultureInfo.InvariantCulture), kv => kv.Value);
        var widths = Column('w');
        var heights = Column('h');
        return new CombatSprites(Column('r'), widths.ToDictionary(kv => kv.Key, kv => (kv.Value, heights[kv.Key])));
    }
}
