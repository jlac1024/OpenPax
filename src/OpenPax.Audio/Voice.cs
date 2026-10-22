namespace OpenPax.Audio;

/// <summary>One sound playing: its samples, where it is in them, and the volume it started at.</summary>
internal sealed class Voice(int id, short[] samples, bool loop, double gain)
{
    public int Id { get; } = id;

    public short[] Samples { get; } = samples;

    public bool Loop { get; } = loop;

    public double Gain { get; } = gain;

    public int Position { get; set; }
}
