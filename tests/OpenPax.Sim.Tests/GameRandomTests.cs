namespace OpenPax.Sim.Tests;

public class GameRandomTests
{
    [Fact]
    public void MatchesTheAnsiCExampleGenerator()
    {
        // The C standard's sample rand() uses the same constants; with seed 1
        // its well-known first outputs are these.
        var random = new GameRandom(1);

        Assert.Equal([16838, 5758, 10113, 17515, 31051], Enumerable.Range(0, 5).Select(_ => random.Next(32768)));
    }

    [Fact]
    public void TakesTheRemainderOfTheFifteenBitValue()
    {
        var a = new GameRandom(1);
        var b = new GameRandom(1);

        Assert.Equal(16838 % 300, a.Next(300));
        Assert.Equal(5758 % 300, a.Next(300));
        // Reseeding restarts the sequence.
        b.Next(7);
        b.Seed(1);
        Assert.Equal(16838 % 7, b.Next(7));
    }

    [Fact]
    public void NegativeLimitsActAsHugeUnsignedDivisors()
    {
        // The original's unsigned divide returns the raw value for them.
        Assert.Equal(16838, new GameRandom(1).Next(-1));
    }

    [Fact]
    public void ZeroLimitIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameRandom(1).Next(0));
    }
}
