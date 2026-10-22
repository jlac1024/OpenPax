namespace OpenPax.Audio;

/// <summary>The Ogg Vorbis tracks, decoded as they play.</summary>
public sealed class OggMusicSource(Func<int, byte[]?> readTrack) : IMusicSource
{
    public IMusicStream? Open(int track)
    {
        return readTrack(track) is { } file ? new OggMusicStream(file) : null;
    }
}
