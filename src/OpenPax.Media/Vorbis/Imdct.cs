namespace OpenPax.Media.Vorbis;

/// <summary>
/// Inverse MDCT of size N (N/2 coefficients in, N samples out), as Vorbis
/// defines it: y[n] = sum over k of X[k] cos(pi/(2N) (2n + 1 + N/2)(2k + 1)).
/// Computed through a DCT-IV of size N/2, itself done with a complex FFT
/// of size N/4.
/// </summary>
internal sealed class Imdct
{
    private readonly int _n;
    private readonly double[] _twiddleCos;
    private readonly double[] _twiddleSin;
    private readonly double[] _postCos;
    private readonly double[] _postSin;
    private readonly double[] _fftCos;
    private readonly double[] _fftSin;
    private readonly int[] _bitReverse;

    // Scratch buffers reused across calls; a decoder is used from one thread.
    private readonly double[] _re;
    private readonly double[] _im;
    private readonly double[] _u;

    public Imdct(int n)
    {
        _n = n;
        var m = n / 2;
        var q = m / 2;
        _twiddleCos = new double[q];
        _twiddleSin = new double[q];
        _postCos = new double[q];
        _postSin = new double[q];
        for (var k = 0; k < q; k++)
        {
            var a = -Math.PI * (k + 0.25) / m;
            _twiddleCos[k] = Math.Cos(a);
            _twiddleSin[k] = Math.Sin(a);
            _postCos[k] = Math.Cos(-Math.PI * k / m);
            _postSin[k] = Math.Sin(-Math.PI * k / m);
        }

        _fftCos = new double[q / 2 + 1];
        _fftSin = new double[q / 2 + 1];
        for (var k = 0; k <= q / 2; k++)
        {
            _fftCos[k] = Math.Cos(-2 * Math.PI * k / q);
            _fftSin[k] = Math.Sin(-2 * Math.PI * k / q);
        }

        _re = new double[q];
        _im = new double[q];
        _u = new double[m];
        _bitReverse = new int[q];
        var bits = VorbisMath.ILog(q) - 1;
        for (var i = 0; i < q; i++)
        {
            var r = 0;
            for (var b = 0; b < bits; b++)
            {
                r |= (i >> b & 1) << (bits - 1 - b);
            }

            _bitReverse[i] = r;
        }
    }

    /// <summary>Transforms <paramref name="input"/> (N/2 values) into <paramref name="output"/> (N values).</summary>
    public void Inverse(ReadOnlySpan<float> input, Span<float> output)
    {
        var m = _n / 2;
        var q = m / 2;
        var re = _re;
        var im = _im;

        // Pre-twiddle into bit-reversed order for an in-place FFT.
        for (var k = 0; k < q; k++)
        {
            double a = input[2 * k];
            double b = input[m - 1 - 2 * k];
            var j = _bitReverse[k];
            re[j] = a * _twiddleCos[k] - b * _twiddleSin[k];
            im[j] = a * _twiddleSin[k] + b * _twiddleCos[k];
        }

        Fft(re, im);

        // Post-twiddle by exp(-i pi k / M) gives u[2k] - i u[M-1-2k] of the
        // DCT-IV; then unfold it into the N outputs.
        var u = _u;
        for (var k = 0; k < q; k++)
        {
            var c = _postCos[k];
            var s = _postSin[k];
            var wr = re[k] * c - im[k] * s;
            var wi = re[k] * s + im[k] * c;
            u[2 * k] = wr;
            u[m - 1 - 2 * k] = -wi;
        }

        var half = m / 2;
        for (var i = 0; i < half; i++)
        {
            output[i] = (float)u[i + half];
        }

        for (var i = half; i < 3 * half; i++)
        {
            output[i] = (float)-u[3 * half - 1 - i];
        }

        for (var i = 3 * half; i < _n; i++)
        {
            output[i] = (float)-u[i - 3 * half];
        }
    }

    private void Fft(double[] re, double[] im)
    {
        var size = re.Length;
        for (var len = 2; len <= size; len <<= 1)
        {
            var step = size / len;
            var halfLen = len / 2;
            for (var start = 0; start < size; start += len)
            {
                for (var k = 0; k < halfLen; k++)
                {
                    var c = _fftCos[k * step];
                    var s = _fftSin[k * step];
                    var a = start + k;
                    var b = a + halfLen;
                    var tr = re[b] * c - im[b] * s;
                    var ti = re[b] * s + im[b] * c;
                    re[b] = re[a] - tr;
                    im[b] = im[a] - ti;
                    re[a] += tr;
                    im[a] += ti;
                }
            }
        }
    }
}
