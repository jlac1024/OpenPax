namespace OpenPax.Media.Video;

/// <summary>
/// FFmpeg's error codes that come from the C library's errno, which differ
/// between platforms: AVERROR(EAGAIN) is -11 on Linux and Windows (MinGW's
/// EAGAIN is Linux's), -35 on macOS. FfmpegAbi is generated on Linux.
/// </summary>
public static class FfmpegErrors
{
    // EAGAIN in <errno.h> on Darwin.
    private const int DarwinAgain = -35;

    public static int Again => OperatingSystem.IsMacOS() ? DarwinAgain : FfmpegAbi.ErrorAgain;
}
