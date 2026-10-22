namespace OpenPax.Host.Tests;

/// <summary>The frame pacer, on a clock the test moves.</summary>
public sealed class FramePacerTests
{
    private ulong _now;
    private readonly List<ulong> _slept = [];

    private FramePacer Pacer() => new(() => _now, ns =>
    {
        _slept.Add(ns);
        _now += ns;
    });

    [Fact]
    public void FramesEndOnTheirDeadlinesWhateverTheWorkTook()
    {
        var pacer = Pacer();
        pacer.SetRate(60);
        var period = pacer.Period;
        pacer.EndFrame();
        var start = _now;
        foreach (var work in new ulong[] { 2_000_000, 9_000_000, 1_000_000 })
        {
            _now += work;
            pacer.EndFrame();
        }

        // Three frames later the clock is three periods on, whatever each frame's work.
        Assert.Equal(start + (3 * period), _now);
    }

    [Fact]
    public void ALateFrameStartsAfreshRatherThanRushing()
    {
        var pacer = Pacer();
        pacer.SetRate(60);
        pacer.EndFrame();
        _now += 100_000_000;
        pacer.EndFrame();
        var after = _now;
        _now += 1_000_000;
        pacer.EndFrame();
        Assert.Equal(after + pacer.Period, _now);
    }

    [Fact]
    public void WithVsyncAFullFrameIsNotWaitedAgain()
    {
        var pacer = Pacer();
        pacer.SetRate(60);
        pacer.EndFrame(vsynced: true);
        _slept.Clear();
        _now += pacer.Period;
        pacer.EndFrame(vsynced: true);
        Assert.Empty(_slept);
        Assert.Equal(1000.0 / 60, pacer.LastFrameMs, 1);
    }
}
