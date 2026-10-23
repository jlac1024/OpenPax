namespace OpenPax.Host.Tests;

public class HostOptionsTests
{
    [Fact]
    public void NoArgumentsMeansRunUntilClosed()
    {
        var options = HostOptions.Parse([], TextWriter.Null);
        Assert.NotNull(options);
        Assert.Null(options.ExitAfterMilliseconds);
        Assert.False(options.IsImport);
    }

    [Fact]
    public void NoMoviesTakesNoValue()
    {
        var options = HostOptions.Parse(["--no-movies", "--key", "F2", "--screenshot", "shot.png"], TextWriter.Null);
        Assert.NotNull(options);
        Assert.True(options.NoMovies);
        Assert.Single(options.Script);
        Assert.Equal("shot.png", options.Screenshot);
        Assert.False(HostOptions.Parse([], TextWriter.Null)!.NoMovies); // no arguments always parse
    }

    [Fact]
    public void ExitAfterIsParsed()
    {
        var options = HostOptions.Parse(["--exit-after-ms", "1500"], TextWriter.Null);
        Assert.Equal(1500, options?.ExitAfterMilliseconds);
    }

    [Fact]
    public void ImportTakesSourceAndDestination()
    {
        var options = HostOptions.Parse(["--import", "/games/gog", "build/data"], TextWriter.Null);
        Assert.Equal("/games/gog", options?.ImportFrom);
        Assert.Equal("build/data", options?.ImportTo);
        Assert.Null(options?.Ffmpeg);
    }

    [Fact]
    public void FfmpegPathIsParsed()
    {
        var options = HostOptions.Parse(["--import", "a", "b", "--ffmpeg", "tools/ffmpeg"], TextWriter.Null);
        Assert.Equal("tools/ffmpeg", options?.Ffmpeg);
    }

    [Fact]
    public void HeadlessGameSettingsAreParsed()
    {
        var options = HostOptions.Parse(
            ["--headless", "4000", "--seed", "77", "--stars", "50", "--players", "6", "--difficulty", "5", "--replay-out", "r.toml", "--click", "3,4", "--key", "F2"],
            TextWriter.Null);
        Assert.Equal((4000, 77u, 50, 6, 5, "r.toml"), (options?.HeadlessTicks, options?.Seed, options?.Stars, options?.Players, options?.Difficulty, options?.ReplayOut));
        Assert.Equal([new ScriptStep(3, 4, null), new ScriptStep(0, 0, OpenPax.Ui.UiCommand.SectorDisplay)], options?.Script);
    }

    [Fact]
    public void ReplaysSurviveSaveAndLoad()
    {
        var path = Path.Combine(Path.GetTempPath(), "openpax-replay-" + Guid.NewGuid() + ".toml");
        try
        {
            var replay = new Headless.Replay(4000000000, 30, 4, 3, [1, 0xFFFFFFFFFFFFFFFF, 0x0123456789ABCDEF]);
            replay.Save(path);
            var loaded = Headless.Replay.Load(path);
            Assert.Equal((replay.Seed, replay.Stars, replay.Players, replay.Difficulty), (loaded.Seed, loaded.Stars, loaded.Players, loaded.Difficulty));
            Assert.Equal(replay.Hashes, loaded.Hashes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("--headless", "many")]
    [InlineData("--click", "3")]
    [InlineData("--import", "only-one")]
    [InlineData("--exit-after-ms")]
    [InlineData("--exit-after-ms", "soon")]
    [InlineData("--exit-after-ms", "-1")]
    [InlineData("--fullscreen")]
    public void BadArgumentsAreRejected(params string[] args)
    {
        Assert.Null(HostOptions.Parse(args, TextWriter.Null));
    }
}
