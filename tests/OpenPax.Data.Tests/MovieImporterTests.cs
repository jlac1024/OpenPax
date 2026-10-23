using OpenPax.Data.Import;
using OpenPax.TestSupport;

namespace OpenPax.Data.Tests;

/// <summary>Converting a movie with the ffmpeg tool, as the import does (on Windows, with no console window).</summary>
public class MovieImporterTests
{
    [Fact]
    public void AMovieConvertsToMp4()
    {
        var game = GameData.RequireFolder();
        var root = GameData.RepositoryRoot();
        var ffmpeg = Path.Combine(root, "build", "native", "ffmpeg-tool", "bin", OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        Assert.SkipWhen(!File.Exists(ffmpeg), "The ffmpeg tool is not built: run build.sh");

        // One small movie in a folder of its own, laid out like the game's.
        var work = Path.Combine(root, "build", "test-movies");
        if (Directory.Exists(work))
        {
            Directory.Delete(work, true);
        }

        try
        {
            var movies = Directory.CreateDirectory(Path.Combine(work, "game", "_Movie")).FullName;
            File.Copy(Path.Combine(game, "_Movie", "Mix_LOGOS.smk"), Path.Combine(movies, "Mix_LOGOS.smk"));
            var log = new StringWriter();
            var progress = new List<(int, int)>();

            var ok = MovieImporter.ImportAll(Path.Combine(work, "game"), Path.Combine(work, "out"), ffmpeg, log, (done, of) => progress.Add((done, of)));

            Assert.True(ok, log.ToString());
            var mp4 = Directory.GetFiles(Path.Combine(work, "out"), "Mix_LOGOS.mp4", SearchOption.AllDirectories);
            Assert.Single(mp4);
            Assert.True(new FileInfo(mp4[0]).Length > 100_000);
            Assert.Equal([(1, 1)], progress);
        }
        finally
        {
            Directory.Delete(work, true);
        }
    }
}
