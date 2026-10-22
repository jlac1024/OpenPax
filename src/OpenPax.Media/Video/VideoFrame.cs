namespace OpenPax.Media.Video;

/// <summary>A decoded picture as RGBA bytes (R, G, B, A per pixel), shown at <see cref="Time"/> seconds.</summary>
public sealed record VideoFrame(double Time, int Width, int Height, byte[] Rgba);
