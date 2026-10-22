namespace OpenPax.Audio;

/// <summary>What the interface asks of sound and music.</summary>
public interface ISoundPlayer
{
    /// <summary>Plays a sound once or for ever. A voice already playing is not started again.</summary>
    void Play(int id, bool loop = false);

    /// <summary>Halts one playing instance of a sound.</summary>
    void Stop(int id);

    /// <summary>Asks for a music track: 2 the title, 3 the setup screens, 14 the game's tracks.</summary>
    void RequestMusic(int track);

    /// <summary>A battle with the local side starts or ends.</summary>
    void BattleMusic(bool on);

    /// <summary>Music stops for a movie and goes on after it.</summary>
    void PauseMusic(bool paused);

    /// <summary>Volumes 0 to 127 for effects, voices and music, as the preferences hold them.</summary>
    void SetVolumes(int effects, int voices, int music);
}
