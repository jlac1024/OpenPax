namespace OpenPax.Audio;

/// <summary>
/// Sound and music mixed in software (the original's Miles quick API,
/// and its neighbors). Effects (ids below 33000) may play over
/// themselves; a voice or ambience loop already playing is not started
/// again. A sound keeps the volume it started at. Everything plays centered.
/// When every voice is busy a new sound is dropped. The host pulls mixed
/// samples as the output needs them; nothing here touches a device.
/// </summary>
public sealed class Mixer(SoundBank bank, MusicPlayer music) : ISoundPlayer
{
    /// <summary>Samples per second of the mix (the music's rate; effects are converted up from 22050).</summary>
    public const int Rate = 44100;

    public const int MaxVoices = 16;

    private const int MaxVolume = 127;

    // The original's master volume, 100 of 127, on every sound.
    private const double Master = 100.0 / MaxVolume;

    private readonly List<Voice> _voices = [];
    private readonly List<short> _movie = [];
    private int _movieAt;
    private short[] _musicBuffer = [];
    private int _effects = 100;
    private int _voiceVolume = 100;
    private int _music = 100;

    public MusicPlayer Music { get; } = music;

    /// <summary>The ids playing now, in the order they started.</summary>
    public IEnumerable<int> Playing => _voices.Select(v => v.Id);

    public void Play(int id, bool loop = false)
    {
        var voice = SoundIds.IsVoice(id);
        if ((voice && IsPlaying(id)) || _voices.Count >= MaxVoices || bank.Get(id) is not { } samples)
        {
            return;
        }

        var gain = (voice ? _voiceVolume : _effects) / (double)MaxVolume * Master;
        _voices.Add(new Voice(id, samples, loop, gain));
    }

    public bool IsPlaying(int id) => _voices.Any(v => v.Id == id);

    public void Stop(int id)
    {
        var i = _voices.FindIndex(v => v.Id == id);
        if (i >= 0)
        {
            _voices.RemoveAt(i);
        }
    }

    public void StopAll() => _voices.Clear();

    public void RequestMusic(int track) => Music.Request(track);

    public void BattleMusic(bool on) => Music.Battle(on);

    public void PauseMusic(bool paused) => Music.Paused = paused;

    /// <summary>
    /// A movie's sound as it is decoded, played in turn at the effects
    /// volume (the original's Smacker volume is the effects volume).
    /// </summary>
    public void QueueMovie(ReadOnlySpan<float> interleaved, int rate, int channels)
    {
        var frames = interleaved.Length / channels;
        var outFrames = (int)((long)frames * Rate / rate);
        var step = (double)rate / Rate;
        for (var i = 0; i < outFrames; i++)
        {
            var at = Math.Min((int)(i * step), frames - 1);
            for (var c = 0; c < 2; c++)
            {
                var v = interleaved[(at * channels) + Math.Min(c, channels - 1)];
                _movie.Add((short)Math.Clamp((int)(v * 32767f), short.MinValue, short.MaxValue));
            }
        }
    }

    /// <summary>A skipped movie's sound stops at once.</summary>
    public void ClearMovie()
    {
        _movie.Clear();
        _movieAt = 0;
    }

    public void SetVolumes(int effects, int voices, int music)
    {
        (_effects, _voiceVolume, _music) = (effects, voices, music);
    }

    /// <summary>Mixes the next interleaved stereo samples; finished sounds are let go.</summary>
    public void Mix(Span<short> output)
    {
        if (_musicBuffer.Length < output.Length)
        {
            _musicBuffer = new short[output.Length];
        }

        var music = _musicBuffer.AsSpan(0, output.Length);
        Music.Read(music);
        var musicGain = _music / (double)MaxVolume;
        var movieGain = _effects / (double)MaxVolume;
        for (var i = 0; i < output.Length; i++)
        {
            var sum = music[i] * musicGain;
            if (_movieAt < _movie.Count)
            {
                sum += _movie[_movieAt++] * movieGain;
            }

            foreach (var v in _voices)
            {
                if (v.Position < v.Samples.Length)
                {
                    sum += v.Samples[v.Position++] * v.Gain;
                }

                if (v.Position >= v.Samples.Length && v.Loop)
                {
                    v.Position = 0;
                }
            }

            output[i] = (short)Math.Clamp((int)sum, short.MinValue, short.MaxValue);
        }

        _voices.RemoveAll(v => !v.Loop && v.Position >= v.Samples.Length);
        if (_movieAt >= _movie.Count)
        {
            ClearMovie();
        }
        else if (_movieAt > Rate * 2)
        {
            _movie.RemoveRange(0, _movieAt);
            _movieAt = 0;
        }
    }
}
