namespace OpenPax.Audio.Tests;

public class MixerTests
{
    [Fact]
    public void EffectsPlayOverThemselvesButAVoiceIsNotStartedAgain()
    {
        var mixer = TestSounds.Mixer();
        mixer.Play(11000);
        mixer.Play(11000);
        mixer.Play(37800);
        mixer.Play(37800);
        Assert.Equal([11000, 11000, 37800], mixer.Playing);
    }

    [Fact]
    public void ASoundEndsAndALoopGoesOn()
    {
        var mixer = TestSounds.Mixer(frames: 50);
        mixer.Play(11000);
        mixer.Play(45100, loop: true);
        mixer.Mix(new short[2 * 1000]);
        Assert.Equal([45100], mixer.Playing);
        mixer.Stop(45100);
        Assert.Empty(mixer.Playing);
    }

    [Fact]
    public void AnIdWithNoFileIsSilentAndAFullMixerDropsTheNewSound()
    {
        var mixer = TestSounds.Mixer();
        mixer.Play(1);
        Assert.Empty(mixer.Playing);
        for (var i = 0; i < Mixer.MaxVoices + 3; i++)
        {
            mixer.Play(11000);
        }

        Assert.Equal(Mixer.MaxVoices, mixer.Playing.Count());
    }

    [Fact]
    public void VolumesScaleEffectsAndVoicesSeparately()
    {
        var mixer = TestSounds.Mixer();
        mixer.SetVolumes(127, 0, 0);
        mixer.Play(11000);
        var buffer = new short[20];
        mixer.Mix(buffer);

        // Upsampled from 22050 by two, at 127 of 127 times the master 100 of 127.
        Assert.Equal((short)(1000 * 100 / 127), buffer[2]);

        var quiet = TestSounds.Mixer();
        quiet.SetVolumes(127, 0, 0);
        quiet.Play(37800);
        var silent = new short[20];
        quiet.Mix(silent);
        Assert.All(silent, s => Assert.Equal(0, s));
    }

    [Fact]
    public void SoundsAreConvertedToStereoAtTheMixRate()
    {
        var pcm = OpenPax.Media.Wav.WavReader.Read(TestSounds.Wav(100));
        var samples = SoundBank.Convert(pcm);
        Assert.Equal(200 * 2, samples.Length);
        Assert.Equal(samples[0], samples[1]);
    }

    [Fact]
    public void EveryIdNamesAGameFile()
    {
        var data = OpenPax.TestSupport.ImportedData.RequireFolder();
        var folder = Path.Combine(data, "original", "Sound");
        var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(folder, f).Replace('\\', '/').ToLowerInvariant()).ToHashSet();
        Assert.All(SoundIds.All, id => Assert.Contains(SoundIds.File(id)!.ToLowerInvariant(), files)); // every id in the table has a path
    }

    [Fact]
    public void AMoviesSoundPlaysAtTheEffectsVolumeUntilCleared()
    {
        var mixer = TestSounds.Mixer();
        mixer.SetVolumes(127, 0, 0);
        mixer.QueueMovie([0.5f, 0.5f, 0.5f], Mixer.Rate / 2, 1);
        var buffer = new short[16];
        mixer.Mix(buffer);

        // Three mono samples at half the rate become six stereo frames.
        Assert.All(buffer[..12], s => Assert.Equal((short)(0.5f * 32767f), s));
        Assert.All(buffer[12..], s => Assert.Equal(0, s));

        mixer.QueueMovie([0.5f, 0.5f], Mixer.Rate, 2);
        mixer.ClearMovie();
        mixer.Mix(buffer);
        Assert.All(buffer, s => Assert.Equal(0, s));
    }
}
