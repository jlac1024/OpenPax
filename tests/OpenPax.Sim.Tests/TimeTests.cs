using OpenPax.Sim.Setup;
using OpenPax.Sim.Time;

namespace OpenPax.Sim.Tests;

public class TimeTests
{
    [Fact]
    public void TasksRunInTheOriginalsOrderOnMultiplesOfTheirPeriod()
    {
        var scheduler = Scheduler.ForNewGame(difficulty: 3, networked: false);
        var runs = new List<(int Tick, TaskKind Kind)>();
        for (var tick = 1; tick <= 4000; tick++)
        {
            scheduler.Tick(kind =>
            {
                runs.Add((tick, kind));
                return true;
            });
        }

        Assert.Equal([TaskKind.ComputerPlayer, TaskKind.StarSystems], runs.Where(r => r.Tick == 5).Select(r => r.Kind));
        Assert.Equal(
            [TaskKind.Autosave, TaskKind.Empire, TaskKind.Espionage, TaskKind.Research, TaskKind.Diplomacy, TaskKind.Advisors,
                TaskKind.PlayerUpdate, TaskKind.Calendar, TaskKind.ComputerPlayer, TaskKind.StarSystems],
            runs.Where(r => r.Tick == 4000).Select(r => r.Kind));
        Assert.Equal(1000, runs.Count(r => r.Kind == TaskKind.Calendar));
        Assert.Equal(20, runs.Count(r => r.Kind == TaskKind.Diplomacy));
        Assert.Equal(1, runs.Count(r => r.Kind == TaskKind.Autosave));
    }

    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 10)]
    [InlineData(3, 5)]
    [InlineData(5, 5)]
    public void ComputerPlayersThinkMoreOftenOnHarderGames(int difficulty, int period)
    {
        Assert.Equal(period, Scheduler.ComputerPlayerPeriod(difficulty));
    }

    [Fact]
    public void NetworkedGamesHaveNoAutosave()
    {
        Assert.DoesNotContain(Scheduler.ForNewGame(1, networked: true).Tasks, t => t.Kind == TaskKind.Autosave);
    }

    [Fact]
    public void TaskReturningFalseLeavesTheList()
    {
        var scheduler = Scheduler.ForNewGame(1, networked: false);
        scheduler.Tick(kind => kind != TaskKind.StarSystems);
        Assert.DoesNotContain(scheduler.Tasks, t => t.Kind == TaskKind.StarSystems);
    }

    [Fact]
    public void CalendarYearIsAHundredSteps()
    {
        var calendar = new Calendar();
        for (var i = 0; i < 250; i++)
        {
            calendar.Advance();
        }

        Assert.Equal((2102, 50), (calendar.Year, calendar.Step));
    }

    [Fact]
    public void ClockStepsOncePerIntervalWithoutCatchingUp()
    {
        var clock = new GameClock(1000);
        Assert.Equal(150, clock.Interval);
        Assert.False(clock.Poll(1149));
        Assert.True(clock.Poll(1150));

        // A long stall gives one step, not several.
        Assert.True(clock.Poll(5000));
        Assert.False(clock.Poll(5001));

        clock.StepNow = true;
        Assert.True(clock.Poll(5002));
        clock.StepNow = false;
        Assert.False(clock.Poll(5149));
        Assert.True(clock.Poll(5150));
    }

    [Theory]
    [InlineData(0, 600)]
    [InlineData(1, 450)]
    [InlineData(2, 450)]
    [InlineData(4, 75)]
    [InlineData(5, 38)]
    [InlineData(6, 13)]
    public void SpeedPicksTheOriginalsInterval(int speed, int interval)
    {
        var clock = new GameClock(0);
        clock.SetSpeed(speed);
        Assert.Equal(interval, clock.Interval);
        clock.SetSpeed(7);
        Assert.Equal(interval, clock.Interval);
    }

    [Fact]
    public void ComputerPlayersTakeTurnsRoundRobin()
    {
        var tables = GameTablesForTests.Load();
        var setup = new GameSetup(1234, 30, 4, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]);
        var game = NewGame.Create(setup, tables);

        var thinkers = new List<int>();
        for (var i = 0; i < 40; i++)
        {
            game.Step();
            if (game.Tick % 5 == 0)
            {
                thinkers.Add(game.LastThinker);
            }
        }

        // Player 0 is the human; the three computer players take turns.
        Assert.Equal([1, 2, 3, 1, 2, 3, 1, 2], thinkers);
        Assert.Equal((2100, 10), (game.Calendar.Year, game.Calendar.Step));
    }

    [Fact]
    public void ReplayingGivesTheSameHashEveryTick()
    {
        var tables = GameTablesForTests.Load();
        var setup = new GameSetup(99, 20, 3, 2, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]);
        var a = NewGame.Create(setup, tables);
        var b = NewGame.Create(setup, tables);
        for (var i = 0; i < 500; i++)
        {
            a.Step();
            b.Step();
            Assert.Equal(a.Hash(), b.Hash());
        }
    }
}
