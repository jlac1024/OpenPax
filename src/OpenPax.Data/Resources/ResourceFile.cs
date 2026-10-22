using System.Buffers.Binary;

namespace OpenPax.Data.Resources;

/// <summary>
/// A resource container (<c>Data/*Data</c>, <c>Image/*.img</c>): one or more
/// types, each with numbered resources. Layout is in docs/formats/resources.md.
/// </summary>
public sealed class ResourceFile
{
    // A real file has a handful of types; a bigger count means this is one
    // of the other "II" files (fonts, PrefData), not a container.
    private const int MaxTypes = 256;
    private const int HeaderSize = 6;

    private readonly byte[] _data;
    private readonly Dictionary<ResourceType, ResourceEntry[]> _entries;

    private ResourceFile(string name, byte[] data, List<ResourceType> types, Dictionary<ResourceType, ResourceEntry[]> entries)
    {
        Name = name;
        _data = data;
        Types = types;
        _entries = entries;
    }

    /// <summary>File name used in error messages.</summary>
    public string Name { get; }

    /// <summary>Types in file order.</summary>
    public IReadOnlyList<ResourceType> Types { get; }

    public static ResourceFile Load(string path)
    {
        return Parse(Path.GetFileName(path), File.ReadAllBytes(path));
    }

    public static ResourceFile Parse(string name, byte[] data)
    {
        if (data.Length < HeaderSize || data[0] != (byte)'I' || data[1] != (byte)'I')
        {
            throw new ResourceFormatException(string.Format(Strings.NotAContainer, name));
        }

        var typeCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(2));
        if (typeCount == 0 || typeCount > MaxTypes)
        {
            throw new ResourceFormatException(string.Format(Strings.NotAContainer, name));
        }

        var types = new List<ResourceType>((int)typeCount);
        var raw = new List<(ResourceType Type, int Id, int Offset)[]>((int)typeCount);
        var pos = HeaderSize;
        for (var t = 0; t < typeCount; t++)
        {
            if (data.Length - pos < 8)
            {
                throw new ResourceFormatException(string.Format(Strings.Truncated, name));
            }

            var type = new ResourceType(BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos)));
            var count = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos + 4));
            pos += 8;
            if (types.Contains(type))
            {
                throw new ResourceFormatException(string.Format(Strings.DuplicateType, name, type));
            }

            if (count > (uint)(data.Length - pos) / 8)
            {
                throw new ResourceFormatException(string.Format(Strings.Truncated, name));
            }

            var list = new (ResourceType, int, int)[count];
            for (var i = 0; i < count; i++)
            {
                var id = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(pos));
                var offset = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(pos + 4));
                pos += 8;
                // The original finds resources by binary search on id, so ids
                // must not decrease. Equal ids do occur (Main.img has two
                // different pictures with id 27000); the search then always
                // lands on the first, which TryGet reproduces.
                if (i > 0 && id < list[i - 1].Item2)
                {
                    throw new ResourceFormatException(string.Format(Strings.UnsortedIds, name, type));
                }

                list[i] = (type, id, offset);
            }

            types.Add(type);
            raw.Add(list);
        }

        var directoryEnd = pos;
        var starts = raw.SelectMany(l => l).Select(e => e.Offset).Append(data.Length).Distinct().Order().ToArray();
        var entries = new Dictionary<ResourceType, ResourceEntry[]>(types.Count);
        for (var t = 0; t < types.Count; t++)
        {
            var list = raw[t];
            var built = new ResourceEntry[list.Length];
            for (var i = 0; i < list.Length; i++)
            {
                var (type, id, offset) = list[i];
                if (offset < directoryEnd || offset > data.Length)
                {
                    throw new ResourceFormatException(string.Format(Strings.BadOffset, name, type, id));
                }

                // starts holds every offset plus the file length, so the
                // next larger value is where this resource ends.
                var at = Array.BinarySearch(starts, offset);
                var end = at + 1 < starts.Length ? starts[at + 1] : data.Length;
                built[i] = new ResourceEntry(id, offset, end - offset);
            }

            entries[types[t]] = built;
        }

        return new ResourceFile(name, data, types, entries);
    }

    /// <summary>Entries of one type in file order, which is increasing id order.</summary>
    public IReadOnlyList<ResourceEntry> Entries(ResourceType type)
    {
        return _entries.TryGetValue(type, out var list) ? list : [];
    }

    public bool TryGet(ResourceType type, int id, out ReadOnlyMemory<byte> body)
    {
        body = default;
        if (!_entries.TryGetValue(type, out var list))
        {
            return false;
        }

        // Lower bound: the first entry whose id is not less than the one
        // asked for. With duplicate ids this is the first of them, as in
        // the original.
        var lo = 0;
        var hi = list.Length;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (list[mid].Id < id)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        if (lo == list.Length || list[lo].Id != id)
        {
            return false;
        }

        body = Body(list[lo]);
        return true;
    }

    public ReadOnlyMemory<byte> Get(ResourceType type, int id)
    {
        if (TryGet(type, id, out var body))
        {
            return body;
        }

        throw new KeyNotFoundException(string.Format(Strings.NoSuchResource, Name, type, id));
    }

    public ReadOnlyMemory<byte> GetByIndex(ResourceType type, int index)
    {
        if (!_entries.TryGetValue(type, out var list))
        {
            throw new KeyNotFoundException(string.Format(Strings.NoSuchType, Name, type));
        }

        return Body(list[index]);
    }

    private ReadOnlyMemory<byte> Body(ResourceEntry e)
    {
        return _data.AsMemory(e.Offset, e.Length);
    }
}
