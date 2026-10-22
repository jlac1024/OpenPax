namespace OpenPax.Media.Vorbis;

/// <summary>
/// A Vorbis codebook (spec section 3): a Huffman code over entries, and
/// optionally a vector of floats per entry for vector quantization.
/// </summary>
internal sealed class Codebook
{
    private const uint SyncPattern = 0x564342;

    // Decode tree: node i has children _tree[2i] and _tree[2i + 1]. A
    // value >= 0 is the next node; a value below 0 is leaf ~entry.
    private readonly int[] _tree;
    private readonly float[]? _vectors;

    private Codebook(int dimensions, int entries, int[] tree, float[]? vectors)
    {
        Dimensions = dimensions;
        Entries = entries;
        _tree = tree;
        _vectors = vectors;
    }

    public int Dimensions { get; }

    public int Entries { get; }

    public bool HasVectors => _vectors is not null;

    public static Codebook Read(BitReader r)
    {
        if (r.Read(24) != SyncPattern)
        {
            throw new InvalidDataException(Strings.BadVorbisSetup);
        }

        var dimensions = (int)r.Read(16);
        var entries = (int)r.Read(24);
        var lengths = new int[entries];
        if (r.Flag())
        {
            // Ordered: runs of entries with increasing lengths.
            var current = 0;
            var length = (int)r.Read(5) + 1;
            while (current < entries)
            {
                var count = (int)r.Read(VorbisMath.ILog(entries - current));
                if (current + count > entries || length > 32)
                {
                    throw new InvalidDataException(Strings.BadVorbisSetup);
                }

                lengths.AsSpan(current, count).Fill(length);
                current += count;
                length++;
            }
        }
        else
        {
            var sparse = r.Flag();
            for (var i = 0; i < entries; i++)
            {
                // Length 0 marks an unused entry.
                lengths[i] = !sparse || r.Flag() ? (int)r.Read(5) + 1 : 0;
            }
        }

        var lookupType = r.Read(4);
        float[]? vectors = null;
        if (lookupType is 1 or 2)
        {
            var min = VorbisMath.Float32Unpack(r.Read(32));
            var delta = VorbisMath.Float32Unpack(r.Read(32));
            var valueBits = (int)r.Read(4) + 1;
            var sequence = r.Flag();
            var lookupValues = lookupType == 1 ? VorbisMath.Lookup1Values(entries, dimensions) : entries * dimensions;
            var multiplicands = new uint[lookupValues];
            for (var i = 0; i < lookupValues; i++)
            {
                multiplicands[i] = r.Read(valueBits);
            }

            vectors = BuildVectors(lookupType, entries, dimensions, lookupValues, multiplicands, min, delta, sequence);
        }
        else if (lookupType != 0)
        {
            throw new InvalidDataException(Strings.BadVorbisSetup);
        }

        if (r.EndOfPacket)
        {
            throw new InvalidDataException(Strings.BadVorbisSetup);
        }

        return new Codebook(dimensions, entries, BuildTree(lengths), vectors);
    }

    /// <summary>Reads one entry number; -1 at end of packet.</summary>
    public int DecodeScalar(BitReader r)
    {
        var node = 0;
        while (true)
        {
            var bit = r.Bit();
            if (bit < 0)
            {
                return -1;
            }

            var next = _tree[2 * node + bit];
            if (next < 0)
            {
                return ~next;
            }

            if (next == 0)
            {
                // A path no codeword uses: treat as a damaged packet.
                return -1;
            }

            node = next;
        }
    }

    /// <summary>The vector of an entry, <see cref="Dimensions"/> values long.</summary>
    public ReadOnlySpan<float> Vector(int entry)
    {
        // Only called for books with lookup tables, which setup checks.
        return _vectors.AsSpan(entry * Dimensions, Dimensions);
    }

    private static float[] BuildVectors(uint type, int entries, int dimensions, int lookupValues, uint[] mult, float min, float delta, bool sequence)
    {
        var vectors = new float[entries * dimensions];
        for (var e = 0; e < entries; e++)
        {
            var last = 0f;
            var divisor = 1;
            for (var j = 0; j < dimensions; j++)
            {
                var offset = type == 1 ? e / divisor % lookupValues : e * dimensions + j;
                var value = mult[offset] * delta + min + last;
                if (sequence)
                {
                    last = value;
                }

                vectors[e * dimensions + j] = value;
                divisor *= lookupValues;
            }
        }

        return vectors;
    }

    /// <summary>
    /// Assigns codewords in entry order, each the numerically lowest free
    /// codeword of its length (spec 3.2.1), and builds the decode tree.
    /// </summary>
    private static int[] BuildTree(int[] lengths)
    {
        var used = lengths.Count(l => l > 0);
        var tree = new List<int> { 0, 0 };
        if (used == 0)
        {
            return [.. tree];
        }

        // next[z]: lowest free codeword at depth z, left-aligned in 32 bits.
        var next = new uint[33];
        var first = Array.FindIndex(lengths, l => l > 0);
        for (var z = 1; z <= lengths[first]; z++)
        {
            next[z] = 1u << (32 - z);
        }

        Insert(tree, 0, lengths[first], first);
        for (var i = first + 1; i < lengths.Length; i++)
        {
            var len = lengths[i];
            if (len == 0)
            {
                continue;
            }

            var z = len;
            while (z > 0 && next[z] == 0)
            {
                z--;
            }

            if (z == 0)
            {
                throw new InvalidDataException(Strings.BadVorbisSetup);
            }

            var code = next[z];
            next[z] = 0;
            for (var y = len; y > z; y--)
            {
                next[y] = code + (1u << (32 - y));
            }

            Insert(tree, code >> (32 - len), len, i);
        }

        return [.. tree];
    }

    private static void Insert(List<int> tree, uint code, int length, int entry)
    {
        var node = 0;
        for (var d = length - 1; d >= 0; d--)
        {
            var slot = 2 * node + (int)(code >> d & 1);
            if (d == 0)
            {
                tree[slot] = ~entry;
                return;
            }

            if (tree[slot] <= 0)
            {
                tree.Add(0);
                tree.Add(0);
                tree[slot] = tree.Count / 2 - 1;
            }

            node = tree[slot];
        }
    }
}
