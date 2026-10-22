namespace OpenPax.Media.Video;

/// <summary>Decoded movie sound: interleaved samples in the range -1 to 1, starting at <see cref="Time"/> seconds.</summary>
public sealed record AudioChunk(double Time, int SampleRate, int Channels, float[] Samples);
