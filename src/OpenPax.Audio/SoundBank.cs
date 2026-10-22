using OpenPax.Media.Wav;

namespace OpenPax.Audio;

/// <summary>
/// The sounds by id, read once and kept converted to the mix format
/// . The original loads whole files and drops the
/// oldest past 2 MB; the port keeps them all, which is far less memory than
/// anything else it holds. A missing file stays silent.
/// </summary>
public sealed class SoundBank(Func<string, byte[]?> readSoundFile)
{
    private readonly Dictionary<int, short[]?> _cache = [];

    /// <summary>Interleaved stereo 16-bit samples at <see cref="Mixer.Rate"/>, or null for an id without a file.</summary>
    public short[]? Get(int id)
    {
        if (_cache.TryGetValue(id, out var samples))
        {
            return samples;
        }

        samples = SoundIds.File(id) is { } path && readSoundFile(path) is { } file ? Convert(WavReader.Read(file)) : null;
        _cache[id] = samples;
        return samples;
    }

    /// <summary>To stereo at the mix rate, by straight lines between samples.</summary>
    public static short[] Convert(PcmSound sound)
    {
        var source = sound.ToInt16();
        var channels = sound.Channels;
        var frames = source.Length / channels;
        var outFrames = (int)((long)frames * Mixer.Rate / sound.SampleRate);
        var output = new short[outFrames * 2];
        var step = (double)sound.SampleRate / Mixer.Rate;
        for (var i = 0; i < outFrames; i++)
        {
            var at = i * step;
            var a = (int)at;
            var b = Math.Min(a + 1, frames - 1);
            var t = at - a;
            for (var c = 0; c < 2; c++)
            {
                var sc = Math.Min(c, channels - 1);
                output[(i * 2) + c] = (short)Math.Round((source[(a * channels) + sc] * (1 - t)) + (source[(b * channels) + sc] * t));
            }
        }

        return output;
    }
}
