using OpenPax.Media.Ogg;

namespace OpenPax.Media.Vorbis;

/// <summary>
/// Decodes an Ogg Vorbis file (Vorbis I spec) to floating point samples.
/// Used for the music, which GOG ships as Ogg Vorbis rips of the CD tracks.
/// </summary>
public sealed class VorbisDecoder
{
    private readonly VorbisSetup _setup;
    private readonly IEnumerator<OggPacket> _packets;
    private readonly Imdct _shortImdct;
    private readonly Imdct _longImdct;
    private float[][]? _previous;
    private int _previousSize;
    private long _samplesOut;
    private bool _done;
    private readonly Dictionary<(int, bool, bool), float[]> _windows = [];

    public VorbisDecoder(byte[] oggFile)
    {
        _packets = OggReader.Packets(oggFile).GetEnumerator();
        var headers = new byte[3][];
        for (var i = 0; i < 3; i++)
        {
            if (!_packets.MoveNext())
            {
                throw new InvalidDataException(Strings.NotVorbis);
            }

            headers[i] = _packets.Current.Data;
        }

        _setup = VorbisSetup.Read(headers[0], headers[1], headers[2]);
        _shortImdct = new Imdct(_setup.ShortBlock);
        _longImdct = new Imdct(_setup.LongBlock);
    }

    public int Channels => _setup.Channels;

    public int SampleRate => _setup.SampleRate;

    /// <summary>Decodes the whole stream into interleaved samples in the range -1 to 1.</summary>
    public float[] DecodeAll()
    {
        var output = new float[1 << 20];
        var used = 0;
        while (NextBlock() is { } block)
        {
            var frames = block[0].Length;
            if (used + frames * Channels > output.Length)
            {
                Array.Resize(ref output, Math.Max(output.Length * 2, used + frames * Channels));
            }

            for (var i = 0; i < frames; i++)
            {
                for (var c = 0; c < Channels; c++)
                {
                    output[used++] = block[c][i];
                }
            }
        }

        Array.Resize(ref output, used);
        return output;
    }

    /// <summary>The next finished samples, one array per channel; null at the end.</summary>
    public float[][]? NextBlock()
    {
        while (!_done && _packets.MoveNext())
        {
            var packet = _packets.Current;
            var block = DecodeAudioPacket(packet.Data);
            if (block is null)
            {
                continue;
            }

            var finished = Overlap(block);
            if (packet.IsLastOfStream)
            {
                _done = true;
            }

            // The end of stream page's granule position is the true length;
            // the last block is cut to it.
            if (packet.GranulePosition >= 0 && finished is not null && packet.IsLastOfStream)
            {
                var allowed = (int)Math.Max(0, packet.GranulePosition - _samplesOut);
                if (allowed < finished[0].Length)
                {
                    for (var c = 0; c < finished.Length; c++)
                    {
                        Array.Resize(ref finished[c], allowed);
                    }
                }
            }

            if (finished is not null)
            {
                _samplesOut += finished[0].Length;
                return finished;
            }
        }

        return null;
    }

    /// <summary>Spec 4.3: decodes one audio packet into windowed time-domain blocks.</summary>
    private float[][]? DecodeAudioPacket(byte[] data)
    {
        var r = new BitReader(data);
        if (r.Flag())
        {
            // Not an audio packet; ignored.
            return null;
        }

        var modeBits = VorbisMath.ILog(_setup.Modes.Length - 1);
        var modeNumber = (int)r.Read(modeBits);
        if (modeNumber >= _setup.Modes.Length)
        {
            return null;
        }

        var mode = _setup.Modes[modeNumber];
        var n = mode.LongBlock ? _setup.LongBlock : _setup.ShortBlock;
        var previousLong = false;
        var nextLong = false;
        if (mode.LongBlock)
        {
            previousLong = r.Flag();
            nextLong = r.Flag();
        }

        if (r.EndOfPacket)
        {
            return null;
        }

        var mapping = _setup.Mappings[mode.Mapping];
        var channels = _setup.Channels;
        var half = n / 2;
        var floors = new int[]?[channels];
        for (var c = 0; c < channels; c++)
        {
            floors[c] = _setup.Floors[mapping.SubmapFloor[mapping.Mux[c]]].Decode(r, _setup.Codebooks);
        }

        // A channel coupled with a channel that has audio must be decoded too.
        var noResidue = floors.Select(f => f is null).ToArray();
        for (var i = 0; i < mapping.Magnitude.Length; i++)
        {
            var m = mapping.Magnitude[i];
            var a = mapping.Angle[i];
            if (!noResidue[m] || !noResidue[a])
            {
                noResidue[m] = noResidue[a] = false;
            }
        }

        var spectra = new float[channels][];
        for (var c = 0; c < channels; c++)
        {
            spectra[c] = new float[half];
        }

        for (var s = 0; s < mapping.SubmapResidue.Length; s++)
        {
            var members = Enumerable.Range(0, channels).Where(c => mapping.Mux[c] == s).ToArray();
            var vectors = members.Select(c => spectra[c]).ToArray();
            var skip = members.Select(c => noResidue[c]).ToArray();
            _setup.Residues[mapping.SubmapResidue[s]].Decode(r, _setup.Codebooks, vectors, skip, half);
        }

        for (var i = mapping.Magnitude.Length - 1; i >= 0; i--)
        {
            Uncouple(spectra[mapping.Magnitude[i]], spectra[mapping.Angle[i]]);
        }

        var curve = new float[half];
        var window = Window(n, mode.LongBlock, previousLong, nextLong);
        var imdct = mode.LongBlock ? _longImdct : _shortImdct;
        var output = new float[channels][];
        for (var c = 0; c < channels; c++)
        {
            output[c] = new float[n];
            var floor = floors[c];
            if (floor is null)
            {
                // No floor means silence for this channel in this block.
                continue;
            }

            Array.Clear(curve);
            _setup.Floors[mapping.SubmapFloor[mapping.Mux[c]]].Synthesize(floor, curve);
            for (var i = 0; i < half; i++)
            {
                spectra[c][i] *= curve[i];
            }

            imdct.Inverse(spectra[c], output[c]);
            for (var i = 0; i < n; i++)
            {
                output[c][i] *= window[i];
            }
        }

        return output;
    }

    /// <summary>Spec 4.3.5: square polar channel decoupling.</summary>
    private static void Uncouple(float[] magnitude, float[] angle)
    {
        for (var i = 0; i < magnitude.Length; i++)
        {
            var m = magnitude[i];
            var a = angle[i];
            float newM, newA;
            if (m > 0)
            {
                if (a > 0)
                {
                    newM = m;
                    newA = m - a;
                }
                else
                {
                    newA = m;
                    newM = m + a;
                }
            }
            else
            {
                if (a > 0)
                {
                    newM = m;
                    newA = m + a;
                }
                else
                {
                    newA = m;
                    newM = m - a;
                }
            }

            magnitude[i] = newM;
            angle[i] = newA;
        }
    }

    /// <summary>
    /// Spec 4.3.6 to 4.3.7: returns the samples from the previous block's
    /// center to this block's center, adding the overlapping halves. The
    /// first block only primes the overlap and returns nothing.
    /// </summary>
    private float[][]? Overlap(float[][] block)
    {
        var size = block[0].Length;
        var previous = _previous;
        var previousSize = _previousSize;
        _previous = block;
        _previousSize = size;
        if (previous is null)
        {
            return null;
        }

        var count = previousSize / 4 + size / 4;
        var shift = size / 4 - previousSize / 4;
        var result = new float[block.Length][];
        for (var c = 0; c < block.Length; c++)
        {
            result[c] = new float[count];
            for (var j = 0; j < count; j++)
            {
                var p = previousSize / 2 + j;
                var q = shift + j;
                var sample = 0f;
                if (p < previousSize)
                {
                    sample += previous[c][p];
                }

                if (q >= 0 && q < size)
                {
                    sample += block[c][q];
                }

                result[c][j] = sample;
            }
        }

        return result;
    }

    private float[] Window(int n, bool longBlock, bool previousLong, bool nextLong)
    {
        // Short blocks ignore the flags, so there are at most five shapes.
        var key = longBlock ? (n, previousLong, nextLong) : (n, true, true);
        if (_windows.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var w = new float[n];
        _windows[key] = w;
        var shortBlock = _setup.ShortBlock;
        int leftStart, leftEnd, leftN, rightStart, rightEnd, rightN;
        if (longBlock && !previousLong)
        {
            leftStart = n / 4 - shortBlock / 4;
            leftEnd = n / 4 + shortBlock / 4;
            leftN = shortBlock / 2;
        }
        else
        {
            leftStart = 0;
            leftEnd = n / 2;
            leftN = n / 2;
        }

        if (longBlock && !nextLong)
        {
            rightStart = n * 3 / 4 - shortBlock / 4;
            rightEnd = n * 3 / 4 + shortBlock / 4;
            rightN = shortBlock / 2;
        }
        else
        {
            rightStart = n / 2;
            rightEnd = n;
            rightN = n / 2;
        }

        for (var i = leftStart; i < leftEnd; i++)
        {
            var s = Math.Sin((i - leftStart + 0.5) / leftN * Math.PI / 2);
            w[i] = (float)Math.Sin(Math.PI / 2 * s * s);
        }

        for (var i = leftEnd; i < rightStart; i++)
        {
            w[i] = 1f;
        }

        for (var i = rightStart; i < rightEnd; i++)
        {
            var s = Math.Sin((i - rightStart + 0.5) / rightN * Math.PI / 2 + Math.PI / 2);
            w[i] = (float)Math.Sin(Math.PI / 2 * s * s);
        }

        return w;
    }
}
