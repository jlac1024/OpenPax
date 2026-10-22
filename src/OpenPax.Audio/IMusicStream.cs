namespace OpenPax.Audio;

/// <summary>One track playing.</summary>
public interface IMusicStream
{
    /// <summary>Fills interleaved stereo samples at <see cref="Mixer.Rate"/>; returns the frames written, fewer once the track ends.</summary>
    int Read(Span<short> stereo);
}
