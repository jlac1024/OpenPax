using OpenPax.Media.Vorbis;

namespace OpenPax.Audio;

/// <summary>A Vorbis track decoded block by block and handed out as 16-bit stereo, resampled if it is not at the mix rate.</summary>
public sealed class OggMusicStream(byte[] file) : IMusicStream
{
    private readonly VorbisDecoder _decoder = new(file);
    private float[][]? _block;
    private double _at;
    private bool _ended;

    public int Read(Span<short> stereo)
    {
        var frames = stereo.Length / 2;
        var step = (double)_decoder.SampleRate / Mixer.Rate;
        var written = 0;
        while (written < frames && !_ended)
        {
            if (_block is null || _at >= _block[0].Length)
            {
                if (_block is not null)
                {
                    _at -= _block[0].Length;
                }

                _block = _decoder.NextBlock();
                if (_block is null)
                {
                    _ended = true;
                    break;
                }

                continue;
            }

            var i = (int)_at;
            for (var c = 0; c < 2; c++)
            {
                var v = _block[Math.Min(c, _block.Length - 1)][i];
                stereo[(written * 2) + c] = (short)Math.Clamp((int)(v * 32767f), short.MinValue, short.MaxValue);
            }

            written++;
            _at += step;
        }

        return written;
    }
}
