using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class HistoryTests
{
    [Fact]
    public void EveryTwoHundredTicksEachPlayerRecordsItsStatistics()
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(new GameSetup(1234, 30, 2, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        var p = game.Players[0];
        Assert.All(p.History.Series(EmpireHistory.Score), v => Assert.Equal(EmpireHistory.Empty, v));

        StepEvents events = StepEvents.None;
        for (var i = 0; i < 200; i++)
        {
            events = game.Step() | game.TakeEvents(p);
        }

        Assert.True(events.HasFlag(StepEvents.HistoryRecorded));
        Assert.Equal(X87.ToInt(p.Money), p.History.Series(EmpireHistory.Money)[0]);
        Assert.Equal(1, p.History.Series(EmpireHistory.Colonies)[0]);
        Assert.Equal(p.KnownCount, p.History.Series(EmpireHistory.TechsKnown)[0]);
        Assert.Equal(EmpireHistory.Empty, p.History.Series(EmpireHistory.Money)[1]);
    }

    [Fact]
    public void AFullSeriesDropsItsOldestSample()
    {
        var history = new EmpireHistory();
        for (var i = 0; i < EmpireHistory.Samples + 3; i++)
        {
            history.Append(EmpireHistory.Money, i);
        }

        Assert.Equal(3, history.Series(EmpireHistory.Money)[0]);
        Assert.Equal(EmpireHistory.Samples + 2, history.Series(EmpireHistory.Money)[^1]);
    }
}
