using OpenPax.Sim.Time;

namespace OpenPax.Sim.Tests;

/// <summary>Speed 0: the original's slowest speed, or with the port's toggle a pause.</summary>
public sealed class GameClockZeroTests
{
    [Fact]
    public void SpeedZeroIsSlowInTheOriginalAndHeldWithTheToggle()
    {
        var clock = new GameClock(0);
        clock.SetSpeed(0);
        Assert.True(clock.Poll(600)); // 600 ms a step, as the original
        clock.ZeroPauses = true;
        Assert.True(clock.Held);
        Assert.False(clock.Poll(5000));
        clock.InCombat = true;
        Assert.False(clock.Poll(10000)); // combat holds too
        clock.SetSpeed(1);
        Assert.False(clock.Held);
        Assert.True(clock.Poll(20000));
    }
}
