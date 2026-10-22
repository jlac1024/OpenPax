using System.Diagnostics;
using OpenPax.Media.Video;

namespace OpenPax.Media.Tests;

public class MovieDecoderTests
{
    private const int ComparedFrame = 10;

    [Fact]
    public void DecodesAConvertedMovieLikeFfmpeg()
    {
        var movie = ConvertedMovie("Colonize.mp4");
        var ffmpeg = Tool();
        Assert.SkipWhen(movie is null, "build/data/movies not found; run ./build.sh");
        Assert.SkipWhen(FfmpegForTests.LibraryFolder is null || ffmpeg is null, "FFmpeg not built; run ./build.sh");

        // Checked by the skips above.
        using var decoder = new MovieDecoder(movie!);
        Assert.Equal(640, decoder.Width);
        Assert.Equal(288, decoder.Height);
        Assert.True(decoder.HasAudio);

        var frames = new List<VideoFrame>();
        AudioChunk? firstAudio = null;
        while (frames.Count <= ComparedFrame && decoder.Next() is { } item)
        {
            if (item is VideoFrame f)
            {
                frames.Add(f);
            }
            else if (item is AudioChunk a)
            {
                firstAudio ??= a;
            }
        }

        Assert.Equal(22050, firstAudio?.SampleRate);
        Assert.Equal(2, firstAudio?.Channels);
        Assert.True(frames[1].Time > frames[0].Time);

        var ours = frames[ComparedFrame].Rgba;
        var reference = ReferenceFrame(ffmpeg!, movie!);
        Assert.Equal(640 * 288 * 3, reference.Length);
        long total = 0;
        for (var i = 0; i < 640 * 288; i++)
        {
            for (var c = 0; c < 3; c++)
            {
                total += Math.Abs(ours[i * 4 + c] - reference[i * 3 + c]);
            }
        }

        // Rounding in the color conversion differs slightly from FFmpeg's;
        // a wrong matrix or range would be off by tens.
        var mean = (double)total / (640 * 288 * 3);
        Assert.True(mean < 1.5, $"mean difference {mean:F2}");
    }

    [Fact]
    public void EveryConvertedMovieDecodes()
    {
        var any = ConvertedMovie("Colonize.mp4");
        Assert.SkipWhen(any is null || FfmpegForTests.LibraryFolder is null, "Converted movies or FFmpeg not built; run ./build.sh");

        // Checked by the skip above.
        var files = Directory.GetFiles(Path.GetDirectoryName(any!)!, "*.mp4");
        Assert.Equal(9, files.Length);
        foreach (var file in files)
        {
            using var decoder = new MovieDecoder(file);
            object? item;
            do
            {
                item = decoder.Next();
            }
            while (item is not null and not VideoFrame);

            var frame = Assert.IsType<VideoFrame>(item);
            Assert.Equal(640, frame.Width);
        }
    }

    private static byte[] ReferenceFrame(string ffmpeg, string movie)
    {
        var info = new ProcessStartInfo(ffmpeg) { RedirectStandardOutput = true };
        foreach (var a in (string[])["-v", "error", "-i", movie, "-vf", $"select=eq(n\\,{ComparedFrame})", "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "-"])
        {
            info.ArgumentList.Add(a);
        }

        using var p = Process.Start(info) ?? throw new InvalidOperationException("ffmpeg did not start");
        using var bytes = new MemoryStream();
        p.StandardOutput.BaseStream.CopyTo(bytes);
        p.WaitForExit();
        return bytes.ToArray();
    }

    private static string? ConvertedMovie(string name)
    {
        var root = Root();
        var path = root is null ? null : Path.Combine(root, "build", "data", "movies", name);
        return path is not null && File.Exists(path) ? path : null;
    }

    private static string? Tool()
    {
        var root = Root();
        var path = root is null ? null : Path.Combine(root, "build", "native", "ffmpeg-tool", "bin", "ffmpeg");
        return path is not null && File.Exists(path) ? path : null;
    }

    private static string? Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OpenPax.slnx")))
            {
                return dir.FullName;
            }
        }

        return null;
    }
}
