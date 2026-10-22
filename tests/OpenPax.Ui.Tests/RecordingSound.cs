using OpenPax.Audio;

namespace OpenPax.Ui.Tests;

/// <summary>Remembers what the interface asked of sound and music.</summary>
internal sealed class RecordingSound : ISoundPlayer
{
    public List<int> Played { get; } = [];

    public List<int> Stopped { get; } = [];

    public List<int> Music { get; } = [];

    public List<bool> Battles { get; } = [];

    public (int Effects, int Voices, int Music) Volumes { get; private set; }

    public void Play(int id, bool loop = false) => Played.Add(id);

    public void Stop(int id) => Stopped.Add(id);

    public void RequestMusic(int track) => Music.Add(track);

    public void BattleMusic(bool on) => Battles.Add(on);

    public void PauseMusic(bool paused)
    {
    }

    public void SetVolumes(int effects, int voices, int music) => Volumes = (effects, voices, music);
}
