namespace OpenPax.Media.Vorbis;

/// <summary>
/// Floor type 1 (spec section 7): a piecewise linear spectral envelope.
/// Floor type 0 is obsolete and refused by the setup reader.
/// </summary>
internal sealed class Floor1
{
    private static readonly int[] _ranges = [256, 128, 86, 64];

    private readonly int[] _partitionClasses;
    private readonly int[] _classDimensions;
    private readonly int[] _classSubclasses;
    private readonly int[] _classMasterbooks;
    private readonly int[][] _subclassBooks;
    private readonly int _multiplier;
    private readonly int[] _x;
    private readonly int[] _sorted;
    private readonly int[] _low;
    private readonly int[] _high;

    private Floor1(int[] partitionClasses, int[] dims, int[] subclasses, int[] masterbooks, int[][] subBooks, int multiplier, int[] x)
    {
        _partitionClasses = partitionClasses;
        _classDimensions = dims;
        _classSubclasses = subclasses;
        _classMasterbooks = masterbooks;
        _subclassBooks = subBooks;
        _multiplier = multiplier;
        _x = x;
        _sorted = [.. Enumerable.Range(0, x.Length).OrderBy(i => x[i])];
        _low = new int[x.Length];
        _high = new int[x.Length];
        for (var i = 2; i < x.Length; i++)
        {
            int lo = 0, hi = 1, loX = -1, hiX = int.MaxValue;
            for (var j = 0; j < i; j++)
            {
                if (x[j] < x[i] && x[j] > loX)
                {
                    lo = j;
                    loX = x[j];
                }

                if (x[j] > x[i] && x[j] < hiX)
                {
                    hi = j;
                    hiX = x[j];
                }
            }

            _low[i] = lo;
            _high[i] = hi;
        }
    }

    public static Floor1 Read(BitReader r, int codebookCount)
    {
        var partitions = (int)r.Read(5);
        var classes = new int[partitions];
        var maxClass = -1;
        for (var i = 0; i < partitions; i++)
        {
            classes[i] = (int)r.Read(4);
            maxClass = Math.Max(maxClass, classes[i]);
        }

        var dims = new int[maxClass + 1];
        var subclasses = new int[maxClass + 1];
        var masterbooks = new int[maxClass + 1];
        var subBooks = new int[maxClass + 1][];
        for (var c = 0; c <= maxClass; c++)
        {
            dims[c] = (int)r.Read(3) + 1;
            subclasses[c] = (int)r.Read(2);
            if (subclasses[c] > 0)
            {
                masterbooks[c] = CheckBook((int)r.Read(8), codebookCount);
            }

            subBooks[c] = new int[1 << subclasses[c]];
            for (var j = 0; j < subBooks[c].Length; j++)
            {
                var book = (int)r.Read(8) - 1;
                subBooks[c][j] = book < 0 ? -1 : CheckBook(book, codebookCount);
            }
        }

        var multiplier = (int)r.Read(2) + 1;
        var rangeBits = (int)r.Read(4);
        var x = new List<int> { 0, 1 << rangeBits };
        foreach (var c in classes)
        {
            for (var j = 0; j < dims[c]; j++)
            {
                x.Add((int)r.Read(rangeBits));
            }
        }

        if (x.Count > 65 || x.Distinct().Count() != x.Count)
        {
            throw new InvalidDataException(Strings.BadVorbisSetup);
        }

        return new Floor1(classes, dims, subclasses, masterbooks, subBooks, multiplier, [.. x]);
    }

    /// <summary>
    /// Decodes this channel's floor values (spec 7.2.3). Returns null when
    /// the channel is unused in this packet or the packet ended early.
    /// </summary>
    public int[]? Decode(BitReader r, Codebook[] books)
    {
        if (!r.Flag())
        {
            return null;
        }

        var range = _ranges[_multiplier - 1];
        var y = new int[_x.Length];
        var bits = VorbisMath.ILog(range - 1);
        y[0] = (int)r.Read(bits);
        y[1] = (int)r.Read(bits);
        var offset = 2;
        foreach (var c in _partitionClasses)
        {
            var cdim = _classDimensions[c];
            var cbits = _classSubclasses[c];
            var csub = (1 << cbits) - 1;
            var cval = 0;
            if (cbits > 0)
            {
                cval = books[_classMasterbooks[c]].DecodeScalar(r);
                if (cval < 0)
                {
                    return null;
                }
            }

            for (var j = 0; j < cdim; j++)
            {
                var book = _subclassBooks[c][cval & csub];
                cval >>= cbits;
                if (book >= 0)
                {
                    var v = books[book].DecodeScalar(r);
                    if (v < 0)
                    {
                        return null;
                    }

                    y[offset + j] = v;
                }
            }

            offset += cdim;
        }

        return r.EndOfPacket ? null : y;
    }

    /// <summary>Turns decoded floor values into gains for <paramref name="curve"/> (spec 7.2.4).</summary>
    public void Synthesize(int[] y, Span<float> curve)
    {
        var n = curve.Length;
        var range = _ranges[_multiplier - 1];
        var finalY = new int[y.Length];
        var used = new bool[y.Length];
        finalY[0] = y[0];
        finalY[1] = y[1];
        used[0] = used[1] = true;
        for (var i = 2; i < y.Length; i++)
        {
            var lo = _low[i];
            var hi = _high[i];
            var predicted = RenderPoint(_x[lo], finalY[lo], _x[hi], finalY[hi], _x[i]);
            var val = y[i];
            var highRoom = range - predicted;
            var lowRoom = predicted;
            var room = (highRoom < lowRoom ? highRoom : lowRoom) * 2;
            if (val != 0)
            {
                used[lo] = used[hi] = used[i] = true;
                if (val >= room)
                {
                    finalY[i] = highRoom > lowRoom ? val - lowRoom + predicted : predicted - val + highRoom - 1;
                }
                else
                {
                    finalY[i] = (val & 1) != 0 ? predicted - (val + 1) / 2 : predicted + val / 2;
                }
            }
            else
            {
                finalY[i] = predicted;
            }
        }

        var lx = 0;
        var ly = finalY[_sorted[0]] * _multiplier;
        var hx = 0;
        var hy = ly;
        for (var k = 1; k < _sorted.Length; k++)
        {
            var i = _sorted[k];
            if (!used[i])
            {
                continue;
            }

            hy = finalY[i] * _multiplier;
            hx = _x[i];
            RenderLine(lx, ly, hx, hy, curve);
            lx = hx;
            ly = hy;
        }

        if (hx < n)
        {
            RenderLine(hx, hy, n, hy, curve);
        }
    }

    private static int CheckBook(int book, int count)
    {
        return book < count ? book : throw new InvalidDataException(Strings.BadVorbisSetup);
    }

    private static int RenderPoint(int x0, int y0, int x1, int y1, int x)
    {
        var dy = y1 - y0;
        var adx = x1 - x0;
        var err = Math.Abs(dy) * (x - x0);
        var off = err / adx;
        return dy < 0 ? y0 - off : y0 + off;
    }

    private static void RenderLine(int x0, int y0, int x1, int y1, Span<float> v)
    {
        var dy = y1 - y0;
        var adx = x1 - x0;
        var ady = Math.Abs(dy);
        var b = dy / adx;
        var sy = dy < 0 ? b - 1 : b + 1;
        ady -= Math.Abs(b) * adx;
        var y = y0;
        var err = 0;
        if (x0 < v.Length)
        {
            v[x0] = InverseDbTable.Values[Math.Clamp(y, 0, 255)];
        }

        for (var x = x0 + 1; x < x1 && x < v.Length; x++)
        {
            err += ady;
            if (err >= adx)
            {
                err -= adx;
                y += sy;
            }
            else
            {
                y += b;
            }

            v[x] = InverseDbTable.Values[Math.Clamp(y, 0, 255)];
        }
    }
}
