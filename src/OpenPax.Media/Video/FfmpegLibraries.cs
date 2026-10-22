namespace OpenPax.Media.Video;

/// <summary>
/// Names the bindings import the FFmpeg libraries by. The host's platform
/// folder maps them to files, loading them in <see cref="LoadOrder"/>.
/// </summary>
public static class FfmpegLibraries
{
    public const string Avutil = "avutil";
    public const string Avcodec = "avcodec";
    public const string Avformat = "avformat";

    /// <summary>Each library needs the ones before it already loaded.</summary>
    public static IReadOnlyList<string> LoadOrder { get; } = [Avutil, Avcodec, Avformat];

    public static int Major(string name)
    {
        return name switch
        {
            Avutil => FfmpegAbi.AvutilMajor,
            Avcodec => FfmpegAbi.AvcodecMajor,
            Avformat => FfmpegAbi.AvformatMajor,
            _ => throw new ArgumentException(name, nameof(name)),
        };
    }
}
