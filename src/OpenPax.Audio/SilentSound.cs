namespace OpenPax.Audio;

/// <summary>No sound at all: for tests and when sound cannot start.</summary>
public sealed class SilentSound : ISoundPlayer
{
    public static readonly SilentSound Instance = new();

    public void Play(int id, bool loop = false)
    {
    }

    public void Stop(int id)
    {
    }

    public void RequestMusic(int track)
    {
    }

    public void BattleMusic(bool on)
    {
    }

    public void PauseMusic(bool paused)
    {
    }

    public void SetVolumes(int effects, int voices, int music)
    {
    }
}
