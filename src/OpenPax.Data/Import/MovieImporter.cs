using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenPax.Data.Toml;

namespace OpenPax.Data.Import;

/// <summary>
/// Converts the Smacker movies to MP4 (H.264 video, AAC sound) with the
/// ffmpeg tool built from deps/ (docs/decisions.md, video codec). Only the
/// import step runs ffmpeg; the game plays the MP4s with its own build of
/// the FFmpeg decoding libraries.
/// </summary>
public static class MovieImporter
{
    public const string MovieFolder = "movies";
    public const string MovieIndex = "movies.toml";

    // Standard 4:2:0 H.264 so the files play in any player. Full chroma
    // (4:4:4) measured better against the Smacker frames (44.5 dB PSNR
    // against 41.0 dB at CRF 12), but needs the High 4:4:4 profile, which
    // players such as VLC show as a black window.
    // Four encoder threads, one movie at a time: running all nine at once
    // with every core each starved the 8 GB development machine.
    private static readonly string[] _videoArgs = ["-c:v", "libx264", "-preset", "slow", "-crf", "12", "-pix_fmt", "yuv420p", "-threads", "4"];
    private static readonly string[] _audioArgs = ["-c:a", "aac", "-b:a", "192k"];

    /// <summary>Converts every <c>_Movie/*.smk</c>. Returns false if ffmpeg failed on any.</summary>
    /// <param name="done">Told after each movie: how many are done, of how many.</param>
    public static bool ImportAll(string gameFolder, string outFolder, string ffmpeg, TextWriter log, Action<int, int>? done = null)
    {
        var dir = Directory.CreateDirectory(Path.Combine(outFolder, MovieFolder)).FullName;
        var sources = Directory.GetFiles(Path.Combine(gameFolder, "_Movie"))
            .Where(p => p.EndsWith(".smk", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        // A movie already converted with the same settings from a source no
        // newer than it is kept: converting all nine takes many minutes.
        var settings = string.Join(' ', [.. _videoArgs, .. _audioArgs]);
        var previous = PreviousSettings(Path.Combine(dir, MovieIndex));
        var failures = new string?[sources.Length];
        for (var i = 0; i < sources.Length; i++)
        {
            var file = Path.GetFileNameWithoutExtension(sources[i]) + ".mp4";
            var target = Path.Combine(dir, file);
            var current = previous.TryGetValue(file, out var used) && used == settings
                && File.Exists(target) && File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(sources[i]);
            failures[i] = current ? null : Convert(ffmpeg, sources[i], target);
            done?.Invoke(i + 1, sources.Length);
        }

        var index = new StringBuilder("# Movies converted from _Movie/*.smk. The game names movies by their .smk\n# file name; lookups ignore case, as on Windows.\n\n");
        var ok = true;
        for (var i = 0; i < sources.Length; i++)
        {
            var name = Path.GetFileName(sources[i]);
            if (failures[i] is { } error)
            {
                log.WriteLine(string.Format(Strings.ImportMovieFailed, name, error));
                ok = false;
                continue;
            }

            index.Append("[[movie]]\n");
            index.Append(CultureInfo.InvariantCulture, $"original = \"{name}\"\n");
            index.Append(CultureInfo.InvariantCulture, $"file = \"{Path.GetFileNameWithoutExtension(name)}.mp4\"\n");
            index.Append(CultureInfo.InvariantCulture, $"settings = \"{settings}\"\n\n");
        }

        File.WriteAllText(Path.Combine(dir, MovieIndex), index.ToString());
        log.WriteLine(string.Format(Strings.ImportMovies, sources.Length - failures.Count(f => f is not null)));
        return ok;
    }

    private static Dictionary<string, string> PreviousSettings(string indexPath)
    {
        var result = new Dictionary<string, string>();
        if (!File.Exists(indexPath))
        {
            return result;
        }

        foreach (var movie in TomlReader.Tables(TomlReader.Parse(File.ReadAllText(indexPath)), "movie"))
        {
            if (movie.TryGetValue("file", out var f) && f is string file && movie.TryGetValue("settings", out var v) && v is string used)
            {
                result[file] = used;
            }
        }

        return result;
    }

    /// <summary>Runs ffmpeg on one movie; returns null on success or ffmpeg's error output.</summary>
    private static string? Convert(string ffmpeg, string source, string target)
    {
        // Written under another name first so a stopped import never leaves
        // a half movie that looks finished.
        var partial = target + ".part";
        var info = new ProcessStartInfo(ffmpeg)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var arg in (string[])["-v", "error", "-y", "-i", source, .. _videoArgs, .. _audioArgs, "-movflags", "+faststart", "-f", "mp4", partial])
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info);
        if (process is null)
        {
            return Strings.FfmpegDidNotStart;
        }

        var errors = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            File.Delete(partial);
            return errors.Trim();
        }

        File.Move(partial, target, overwrite: true);
        return null;
    }
}
