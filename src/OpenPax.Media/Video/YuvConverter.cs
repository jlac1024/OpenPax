namespace OpenPax.Media.Video;

/// <summary>
/// Planar YUV to RGBA. The converted movies are 4:2:0; 4:4:4 is handled too
/// in case a movie is replaced with one that has full chroma. Coefficients follow the
/// frame's color space tag: BT.709 when tagged so, otherwise BT.601, the
/// default FFmpeg used when it converted the Smacker frames to YUV.
/// </summary>
internal static class YuvConverter
{
    public static unsafe void ToRgba(nint frame, int width, int height, bool fullChroma, byte[] rgba)
    {
        var y = (byte*)Ffmpeg.PointerAt(frame, FfmpegAbi.FrameData);
        var u = (byte*)Ffmpeg.PointerAt(frame, FfmpegAbi.FrameData + sizeof(nint));
        var v = (byte*)Ffmpeg.PointerAt(frame, FfmpegAbi.FrameData + 2 * sizeof(nint));
        var yStride = Ffmpeg.Int32At(frame, FfmpegAbi.FrameLinesize);
        var uStride = Ffmpeg.Int32At(frame, FfmpegAbi.FrameLinesize + 4);
        var vStride = Ffmpeg.Int32At(frame, FfmpegAbi.FrameLinesize + 8);
        var fullRange = Ffmpeg.Int32At(frame, FfmpegAbi.FrameColorRange) == FfmpegAbi.RangeFull;
        var bt709 = Ffmpeg.Int32At(frame, FfmpegAbi.FrameColorspace) == FfmpegAbi.SpaceBt709;

        // Kr and Kb of the color space; the rest follows from them.
        var kr = bt709 ? 0.2126 : 0.299;
        var kb = bt709 ? 0.0722 : 0.114;
        var kg = 1 - kr - kb;
        var yScale = fullRange ? 1.0 : 255.0 / 219.0;
        var cScale = fullRange ? 1.0 : 255.0 / 224.0;
        var yOffset = fullRange ? 0 : 16;
        var rV = 2 * (1 - kr) * cScale;
        var bU = 2 * (1 - kb) * cScale;
        var gU = -2 * (1 - kb) * kb / kg * cScale;
        var gV = -2 * (1 - kr) * kr / kg * cScale;

        // The plane pointers and strides come from a frame FFmpeg just
        // decoded at this width and height, and rgba holds width * height * 4.
        for (var row = 0; row < height; row++)
        {
            var cRow = fullChroma ? row : row / 2;
            var yLine = y + row * yStride;
            var uLine = u + cRow * uStride;
            var vLine = v + cRow * vStride;
            var o = row * width * 4;
            for (var x = 0; x < width; x++)
            {
                var cx = fullChroma ? x : x / 2;
                var luma = (yLine[x] - yOffset) * yScale;
                var cu = uLine[cx] - 128.0;
                var cv = vLine[cx] - 128.0;
                rgba[o] = Clamp(luma + rV * cv);
                rgba[o + 1] = Clamp(luma + gU * cu + gV * cv);
                rgba[o + 2] = Clamp(luma + bU * cu);
                rgba[o + 3] = 255;
                o += 4;
            }
        }
    }

    private static byte Clamp(double v)
    {
        return v <= 0 ? (byte)0 : v >= 255 ? (byte)255 : (byte)(v + 0.5);
    }
}
