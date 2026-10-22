namespace OpenPax.Host.Tests;

/// <summary>A replay written before the move to American spelling, with its "colour" key, still reads.</summary>
public sealed class ReplaySpellingTests
{
    [Fact]
    public void AnOldColourKeyIsRead()
    {
        var path = Path.Combine(Path.GetTempPath(), "openpax-old-replay-" + Guid.NewGuid() + ".toml");
        try
        {
            var replay = new Headless.Replay(1, 20, 3, 2, [1UL, 2UL]) { Humans = [new Headless.ReplayHuman("Ann", 3, 7, 0)] };
            replay.Save(path);
            File.WriteAllText(path, File.ReadAllText(path).Replace("color =", "colour =", StringComparison.Ordinal));
            Assert.Equal(7, Headless.Replay.Load(path).Humans.Single().Color);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
