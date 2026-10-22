using OpenPax.Sim;
using OpenPax.Sim.Players;
using OpenPax.Ui.Screens;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class SoundTests
{
    [Fact]
    public void TheTitleAsksForItsTrackAndTheVolumesFollowThePreferences()
    {
        var app = NewApp();
        var sound = new RecordingSound();
        app.Sound = sound;
        Assert.Equal([2], sound.Music);
        Assert.Equal((app.Preferences.Get(Preferences.SoundFxVolume), app.Preferences.Get(Preferences.VoiceVolume), app.Preferences.Get(Preferences.RedbookVolume)), sound.Volumes);
        app.Preferences.Set(Preferences.VoiceVolume, 12);
        Assert.Equal(12, sound.Volumes.Voices);
    }

    [Fact]
    public void AButtonSoundsOnPressAndOnRelease()
    {
        var app = NewApp();
        var sound = new RecordingSound();
        app.Sound = sound;
        Click(app, 400, 110);
        Assert.Contains(11000, sound.Played);
        Assert.Contains(11100, sound.Played);
    }

    [Fact]
    public void InGameScreensPlayTheGameTracksAndTheBridgeHums()
    {
        var app = InGame();
        var sound = new RecordingSound();
        app.Sound = sound;
        Assert.Contains(14, sound.Music);
        app.Command(UiCommand.Research);
        app.Command(UiCommand.Bridge);
        Assert.Equal(ScreenMode.MainControl, app.Mode);
        Assert.Contains(45100, sound.Played);
        app.Command(UiCommand.Research);
        Assert.Contains(45100, sound.Stopped);
    }

    [Fact]
    public void ABattleWithTheLocalSideCallsBattleStationsAndChangesTheMusic()
    {
        var app = InGame();
        var sound = new RecordingSound();
        app.Sound = sound;
        var session = app.Session!;
        var game = session.Game;
        var computer = game.Players.First(p => p != session.Local);
        var events = StepEvents.None;
        game.SetTreaty(session.Local, computer, Treaty.War, Treaty.InForce, ref events);
        game.SetTreaty(computer, session.Local, Treaty.War, Treaty.InForce, ref events);
        game.StartFleetBattle(session.Local.Fleets.First(f => f.Ships.Count > 0), computer.Fleets.First(f => f.Ships.Count > 0));
        _ = app.Tick(1).ToList();
        Assert.Contains(33500, sound.Played);
        Assert.Equal([true], sound.Battles);

        app.Command(UiCommand.Mode20);
        Assert.Contains(33400, sound.Played);

        game.Battles.Clear();
        _ = app.Tick(2).ToList();
        Assert.Equal([true, false], sound.Battles);
    }
}
