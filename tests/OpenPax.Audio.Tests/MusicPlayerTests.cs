namespace OpenPax.Audio.Tests;

public class MusicPlayerTests
{
    private static void Play(MusicPlayer music, int frames)
    {
        music.Read(new short[frames * 2]);
    }

    [Fact]
    public void TheTitleTrackLoopsAlone()
    {
        var source = new FakeMusic(frames: 10);
        var music = new MusicPlayer(source);
        music.Request(2);
        Play(music, 25);
        Assert.Equal([2, 2, 2], source.Opened);
    }

    [Fact]
    public void TheGameTracksPlayInTurnAndARequestWithinTheSetChangesNothing()
    {
        var source = new FakeMusic(frames: 10);
        var music = new MusicPlayer(source);
        music.Request(3);
        music.Request(14);
        Assert.Equal(7, music.Track);
        music.Request(9);
        Assert.Equal(7, music.Track);
        Play(music, 25);
        Assert.Equal([3, 7, 8, 9], source.Opened);
    }

    [Fact]
    public void BattlesPlayFiveThenFourThenSixAndTheGameMusicGoesOnAfter()
    {
        var source = new FakeMusic(frames: 1000);
        var music = new MusicPlayer(source);
        music.Request(14);
        var tracks = new List<int>();
        for (var i = 0; i < 4; i++)
        {
            music.Battle(true);
            tracks.Add(music.Track);

            // During a battle, the screens' requests are ignored.
            music.Request(14);
            Assert.Equal(tracks[^1], music.Track);
            music.Battle(false);
        }

        Assert.Equal([5, 4, 6, 4], tracks);
        Assert.Equal([7, 5, 8, 4, 9, 6, 10, 4, 11], source.Opened);
    }

    [Fact]
    public void AMissingTrackStopsTheMusic()
    {
        var music = new MusicPlayer(new FakeMusic(10, 2));
        music.Request(2);
        Assert.Equal(0, music.Track);
        Play(music, 5);
    }

    [Fact]
    public void PausedMusicIsSilentAndGoesOnWhereItWas()
    {
        var source = new FakeMusic(frames: 10);
        var music = new MusicPlayer(source);
        music.Request(2);
        music.Paused = true;
        var buffer = new short[8];
        music.Read(buffer);
        Assert.All(buffer, s => Assert.Equal(0, s));
        music.Paused = false;
        music.Read(buffer);
        Assert.Equal(100, buffer[0]);
    }

    [Fact]
    public void TheGameTracksDecodeToSound()
    {
        var data = OpenPax.TestSupport.ImportedData.RequireFolder();
        var file = Directory.EnumerateFiles(Path.Combine(data, "original", "MUSIC")).First(f => Path.GetFileName(f).Equals("Track02.ogg", StringComparison.OrdinalIgnoreCase));
        var stream = new OggMusicSource(_ => File.ReadAllBytes(file)).Open(2)!; // the source always opens a file it is given
        var buffer = new short[2 * Mixer.Rate];
        Assert.Equal(Mixer.Rate, stream.Read(buffer));
        Assert.Contains(buffer, s => s != 0);
    }
}
