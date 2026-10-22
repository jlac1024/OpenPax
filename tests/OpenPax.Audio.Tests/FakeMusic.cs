namespace OpenPax.Audio.Tests;

/// <summary>Tracks of a fixed length of silence, recording which were opened.</summary>
internal sealed class FakeMusic(int frames = 10, params int[] missing) : IMusicSource
{
    public List<int> Opened { get; } = [];

    public IMusicStream? Open(int track)
    {
        if (missing.Contains(track))
        {
            return null;
        }

        Opened.Add(track);
        return new FakeTrack(frames);
    }
}
