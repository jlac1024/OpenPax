using System.Diagnostics;
using OpenPax.Media.Vorbis;

namespace OpenPax.Media.Tests;

public class VorbisDecoderTests
{
    // The two shortest tracks: enough to cover the encoder settings GOG used
    // without making every build slow.
    public static TheoryData<string> Tracks => new() { "Track02.ogg", "Track12.ogg" };

    [Fact]
    public void EveryMusicTrackStartsDecoding()
    {
        var files = Directory.GetFiles(Path.Combine(GameData.RequireFolder(), "MUSIC"), "*.ogg");

        Assert.Equal(13, files.Length);
        Assert.All(files, f => Assert.NotNull(new VorbisDecoder(File.ReadAllBytes(f)).NextBlock()));
    }

    [Theory]
    [MemberData(nameof(Tracks))]
    public void MatchesFfmpegOnTheGameMusic(string track)
    {
        var path = Path.Combine(GameData.RequireFolder(), "MUSIC", track);
        var ffmpeg = FfmpegTool();
        Assert.SkipWhen(ffmpeg is null, "build/native/ffmpeg-tool not built; run ./build.sh");

        var decoder = new VorbisDecoder(File.ReadAllBytes(path));
        var ours = decoder.DecodeAll();
        // Only checked when present; the ffmpeg path is set above.
        var reference = Reference(ffmpeg!, path);

        Assert.Equal(2, decoder.Channels);
        Assert.Equal(reference.Length, ours.Length);
        double signal = 0, noise = 0;
        for (var i = 0; i < ours.Length; i++)
        {
            signal += (double)reference[i] * reference[i];
            var d = reference[i] - ours[i];
            noise += (double)d * d;
        }

        // Both decoders use floats, so tiny rounding differences are
        // expected; anything above that is a decoding bug.
        var snr = 10 * Math.Log10(signal / Math.Max(noise, 1e-30));
        Assert.True(snr > 90, $"{track}: SNR {snr:F1} dB");
    }

    private static float[] Reference(string ffmpeg, string path)
    {
        var info = new ProcessStartInfo(ffmpeg, ["-v", "error", "-i", path, "-f", "f32le", "-acodec", "pcm_f32le", "-"])
        {
            RedirectStandardOutput = true,
        };
        using var process = Process.Start(info) ?? throw new InvalidOperationException("ffmpeg did not start");
        using var bytes = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(bytes);
        process.WaitForExit();
        var raw = bytes.ToArray();
        var samples = new float[raw.Length / 4];
        Buffer.BlockCopy(raw, 0, samples, 0, samples.Length * 4);
        return samples;
    }

    private static string? FfmpegTool()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var tool = Path.Combine(dir.FullName, "build", "native", "ffmpeg-tool", "bin", "ffmpeg");
            if (File.Exists(tool))
            {
                return tool;
            }
        }

        return null;
    }
}
