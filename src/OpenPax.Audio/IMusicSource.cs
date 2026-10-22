namespace OpenPax.Audio;

/// <summary>Where the music tracks come from (GOG's MUSIC/TrackNN.ogg in place of the CD).</summary>
public interface IMusicSource
{
    /// <summary>A track from its start, or null when it cannot be had.</summary>
    IMusicStream? Open(int track);
}
