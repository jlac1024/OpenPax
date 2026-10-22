namespace OpenPax.Media.Vorbis;

/// <summary>Residue types 0, 1 and 2 (spec section 8): the fine spectral detail.</summary>
internal sealed class Residue
{
    private readonly int _type;
    private readonly int _begin;
    private readonly int _end;
    private readonly int _partitionSize;
    private readonly int _classifications;
    private readonly int _classbook;
    private readonly int[,] _books;

    private Residue(int type, int begin, int end, int partitionSize, int classifications, int classbook, int[,] books)
    {
        _type = type;
        _begin = begin;
        _end = end;
        _partitionSize = partitionSize;
        _classifications = classifications;
        _classbook = classbook;
        _books = books;
    }

    public static Residue Read(BitReader r, int type, Codebook[] books)
    {
        var begin = (int)r.Read(24);
        var end = (int)r.Read(24);
        var partitionSize = (int)r.Read(24) + 1;
        var classifications = (int)r.Read(6) + 1;
        var classbook = (int)r.Read(8);
        if (classbook >= books.Length)
        {
            throw new InvalidDataException(Strings.BadVorbisSetup);
        }

        var cascade = new int[classifications];
        for (var i = 0; i < classifications; i++)
        {
            var low = (int)r.Read(3);
            var high = r.Flag() ? (int)r.Read(5) : 0;
            cascade[i] = high * 8 + low;
        }

        var bookTable = new int[classifications, 8];
        for (var i = 0; i < classifications; i++)
        {
            for (var j = 0; j < 8; j++)
            {
                bookTable[i, j] = -1;
                if ((cascade[i] & 1 << j) != 0)
                {
                    var b = (int)r.Read(8);
                    if (b >= books.Length || !books[b].HasVectors)
                    {
                        throw new InvalidDataException(Strings.BadVorbisSetup);
                    }

                    bookTable[i, j] = b;
                }
            }
        }

        return new Residue(type, begin, end, partitionSize, classifications, classbook, bookTable);
    }

    /// <summary>
    /// Decodes residue vectors for the channels of one submap, adding into
    /// <paramref name="vectors"/> (each n long). Channels with
    /// <paramref name="skip"/> set are not decoded.
    /// </summary>
    public void Decode(BitReader r, Codebook[] books, float[][] vectors, bool[] skip, int n)
    {
        if (_type == 2)
        {
            DecodeType2(r, books, vectors, skip, n);
            return;
        }

        DecodePartitioned(r, books, vectors, skip, n, _type);
    }

    private void DecodeType2(BitReader r, Codebook[] books, float[][] vectors, bool[] skip, int n)
    {
        if (skip.All(s => s))
        {
            return;
        }

        // All channels are decoded as one interleaved vector.
        var ch = vectors.Length;
        var joined = new float[n * ch];
        DecodePartitioned(r, books, [joined], [false], n * ch, 1);
        for (var i = 0; i < n; i++)
        {
            for (var c = 0; c < ch; c++)
            {
                vectors[c][i] += joined[i * ch + c];
            }
        }
    }

    private void DecodePartitioned(BitReader r, Codebook[] books, float[][] vectors, bool[] skip, int actualSize, int format)
    {
        var limitBegin = Math.Min(_begin, actualSize);
        var limitEnd = Math.Min(_end, actualSize);
        var classbook = books[_classbook];
        var perCodeword = classbook.Dimensions;
        var toRead = limitEnd - limitBegin;
        if (toRead <= 0)
        {
            return;
        }

        var partitions = toRead / _partitionSize;
        var classes = new int[vectors.Length, partitions + perCodeword];
        for (var pass = 0; pass < 8; pass++)
        {
            var p = 0;
            while (p < partitions)
            {
                if (pass == 0)
                {
                    for (var j = 0; j < vectors.Length; j++)
                    {
                        if (skip[j])
                        {
                            continue;
                        }

                        var temp = classbook.DecodeScalar(r);
                        if (temp < 0)
                        {
                            return;
                        }

                        for (var i = perCodeword - 1; i >= 0; i--)
                        {
                            classes[j, i + p] = temp % _classifications;
                            temp /= _classifications;
                        }
                    }
                }

                for (var i = 0; i < perCodeword && p < partitions; i++, p++)
                {
                    for (var j = 0; j < vectors.Length; j++)
                    {
                        if (skip[j])
                        {
                            continue;
                        }

                        var book = _books[classes[j, p], pass];
                        if (book < 0)
                        {
                            continue;
                        }

                        var offset = limitBegin + p * _partitionSize;
                        if (!DecodePartition(r, books[book], vectors[j], offset, format))
                        {
                            return;
                        }
                    }
                }
            }
        }
    }

    private bool DecodePartition(BitReader r, Codebook book, float[] v, int offset, int format)
    {
        var dim = book.Dimensions;
        if (format == 0)
        {
            var step = _partitionSize / dim;
            for (var i = 0; i < step; i++)
            {
                var e = book.DecodeScalar(r);
                if (e < 0)
                {
                    return false;
                }

                var vec = book.Vector(e);
                for (var j = 0; j < dim; j++)
                {
                    v[offset + i + j * step] += vec[j];
                }
            }

            return true;
        }

        for (var i = 0; i < _partitionSize;)
        {
            var e = book.DecodeScalar(r);
            if (e < 0)
            {
                return false;
            }

            var vec = book.Vector(e);
            for (var j = 0; j < dim && i < _partitionSize; j++, i++)
            {
                v[offset + i] += vec[j];
            }
        }

        return true;
    }
}
