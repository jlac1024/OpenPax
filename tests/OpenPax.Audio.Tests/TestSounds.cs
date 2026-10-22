namespace OpenPax.Audio.Tests;

/// <summary>Small made-up WAV files for the mixer, so no game data is needed.</summary>
internal static class TestSounds
{
    /// <summary>A 16-bit mono WAV of a constant level at 22050 Hz.</summary>
    public static byte[] Wav(int frames, short level = 1000, int rate = 22050)
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        w.Write("RIFF"u8);
        w.Write(36 + (frames * 2));
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(rate);
        w.Write(rate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8);
        w.Write(frames * 2);
        for (var i = 0; i < frames; i++)
        {
            w.Write(level);
        }

        w.Flush();
        return stream.ToArray();
    }

    /// <summary>A mixer whose every id with a file plays the same short sound, and music from the given source.</summary>
    public static Mixer Mixer(int frames = 100, IMusicSource? music = null)
    {
        var wav = Wav(frames);
        return new Mixer(new SoundBank(_ => wav), new MusicPlayer(music ?? new FakeMusic()));
    }
}
