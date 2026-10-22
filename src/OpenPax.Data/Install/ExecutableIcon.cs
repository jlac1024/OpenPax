using System.Buffers.Binary;
using OpenPax.Data.Art;
using OpenPax.Data.Png;

namespace OpenPax.Data.Install;

/// <summary>
/// The game's icon from the executable's resources (docs/formats/exe-icon.md):
/// the first icon group's largest image, as a picture in its own palette
/// whose last entry is the see-through one. The GOG build holds one, 32x32
/// in 16 colors.
/// </summary>
public static class ExecutableIcon
{
    public const string FileName = "icon.png";

    private const int ResourceDirectory = 2;
    private const int IconType = 3;
    private const int GroupIconType = 14;

    /// <summary>The icon, or null when the executable has none this reader understands (only 1, 4 and 8 bit images).</summary>
    public static IndexedImage? Read(byte[] exe)
    {
        var resources = Resources(exe);
        if (resources is null)
        {
            return null;
        }

        var (sections, root) = resources.Value;
        var groups = Entries(exe, root, root, GroupIconType);
        if (groups.Count == 0)
        {
            return null;
        }

        // The group lists its images: width, height, colors, reserved, planes, bits, size, id (14 bytes each after a 6 byte header).
        var group = Data(exe, sections, root, groups[0]);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(group.AsSpan(4));
        var best = -1;
        var bestArea = -1;
        for (var i = 0; i < count; i++)
        {
            var e = group.AsSpan(6 + (14 * i));
            var w = e[0] == 0 ? 256 : e[0];
            var bits = BinaryPrimitives.ReadUInt16LittleEndian(e[6..]);
            if (bits <= 8 && w * w > bestArea)
            {
                (best, bestArea) = (BinaryPrimitives.ReadUInt16LittleEndian(e[12..]), w * w);
            }
        }

        var image = Entries(exe, root, root, IconType).Select(e => (Id: e.Id, Data: Data(exe, sections, root, e))).FirstOrDefault(e => e.Id == best);
        return image.Data is null ? null : Decode(image.Data);
    }

    /// <summary>A device independent bitmap of an icon: the colors upside down, then a 1 bit mask where set means see-through.</summary>
    private static IndexedImage? Decode(byte[] dib)
    {
        var header = BinaryPrimitives.ReadInt32LittleEndian(dib);
        var width = BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(4));
        var height = BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(8)) / 2;
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(dib.AsSpan(14));
        if (bits is not (1 or 4 or 8) || width <= 0 || height <= 0)
        {
            return null;
        }

        var colors = 1 << bits;
        var palette = new Rgb[colors + 1];
        for (var i = 0; i < colors; i++)
        {
            var c = dib.AsSpan(header + (4 * i));
            palette[i] = new Rgb(c[2], c[1], c[0]);
        }

        var clear = (byte)colors;
        var alpha = Enumerable.Repeat((byte)255, colors).Append((byte)0).ToArray();
        var pixelsAt = header + (4 * colors);
        var stride = ((width * bits) + 31) / 32 * 4;
        var maskAt = pixelsAt + (stride * height);
        var maskStride = (width + 31) / 32 * 4;
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            var row = height - 1 - y;
            for (var x = 0; x < width; x++)
            {
                var bit = x * bits;
                var b = dib[pixelsAt + (row * stride) + (bit / 8)];
                var index = (b >> (8 - bits - (bit % 8))) & (colors - 1);
                var masked = ((dib[maskAt + (row * maskStride) + (x / 8)] >> (7 - (x % 8))) & 1) != 0;
                pixels[(y * width) + x] = masked ? clear : (byte)index;
            }
        }

        return new IndexedImage(width, height, pixels, palette, alpha);
    }

    private static ((uint Va, uint Size, uint Raw)[] Sections, int Root)? Resources(byte[] exe)
    {
        if (exe.Length < 0x40 || exe[0] != 'M' || exe[1] != 'Z')
        {
            return null;
        }

        var pe = BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(0x3C));
        var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan(pe + 6));
        var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan(pe + 20));
        var optional = pe + 24;
        var resourceRva = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(optional + 96 + (ResourceDirectory * 8)));
        var sections = new (uint, uint, uint)[sectionCount];
        for (var i = 0; i < sectionCount; i++)
        {
            var s = exe.AsSpan(optional + optionalSize + (40 * i));
            var virtualSize = BinaryPrimitives.ReadUInt32LittleEndian(s[8..]);
            var va = BinaryPrimitives.ReadUInt32LittleEndian(s[12..]);
            var rawSize = BinaryPrimitives.ReadUInt32LittleEndian(s[16..]);
            sections[i] = (va, Math.Max(virtualSize, rawSize), BinaryPrimitives.ReadUInt32LittleEndian(s[20..]));
        }

        var root = Offset(sections, resourceRva);
        return root < 0 ? null : (sections, root);
    }

    private static int Offset((uint Va, uint Size, uint Raw)[] sections, uint rva)
    {
        foreach (var (va, size, raw) in sections)
        {
            if (rva >= va && rva < va + size)
            {
                return (int)(rva - va + raw);
            }
        }

        return -1;
    }

    /// <summary>The resources of a type: each name's first language, as (id, data entry offset from the root).</summary>
    private static List<(int Id, int Entry)> Entries(byte[] exe, int root, int dir, int type)
    {
        var found = new List<(int, int)>();
        foreach (var (id, sub) in Directory(exe, dir))
        {
            if (id != type || sub >= 0)
            {
                continue;
            }

            foreach (var (name, langs) in Directory(exe, root + (sub & 0x7FFFFFFF)))
            {
                if (langs < 0 && Directory(exe, root + (langs & 0x7FFFFFFF)).FirstOrDefault() is var (_, entry) && entry >= 0)
                {
                    found.Add((name, entry));
                }
            }
        }

        return found;
    }

    /// <summary>A resource directory's entries: id and offset, the offset's top bit set for a further directory.</summary>
    private static IEnumerable<(int Id, int Offset)> Directory(byte[] exe, int at)
    {
        var named = BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan(at + 12));
        var ids = BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan(at + 14));
        for (var i = 0; i < named + ids; i++)
        {
            var e = exe.AsSpan(at + 16 + (8 * i));
            yield return (BinaryPrimitives.ReadInt32LittleEndian(e), BinaryPrimitives.ReadInt32LittleEndian(e[4..]));
        }
    }

    private static byte[] Data(byte[] exe, (uint Va, uint Size, uint Raw)[] sections, int root, (int Id, int Entry) resource)
    {
        var entry = exe.AsSpan(root + resource.Entry);
        var at = Offset(sections, BinaryPrimitives.ReadUInt32LittleEndian(entry));
        var size = BinaryPrimitives.ReadInt32LittleEndian(entry[4..]);
        return at < 0 ? [] : exe.AsSpan(at, size).ToArray();
    }
}
