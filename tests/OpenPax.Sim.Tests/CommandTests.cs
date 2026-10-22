using OpenPax.Sim.Commands;
using OpenPax.Sim.Players;
using OpenPax.Sim.Saving;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class CommandTests
{
    private static Game NewGameForCommands()
    {
        var tables = GameTablesForTests.Load();
        return NewGame.Create(new GameSetup(4321, 30, 3, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
    }

    private static T RoundTrip<T>(T command, GameTables tables)
        where T : Command
    {
        using var stream = new MemoryStream();
        SaveFile.Write(stream, command, tables);
        stream.Position = 0;
        return Assert.IsType<T>(SaveFile.Read(stream, tables, null));
    }

    [Fact]
    public void CommandsTravelInTheSaveFormat()
    {
        var game = NewGameForCommands();
        var order = RoundTrip(new FleetOrderCommand(0, 17, FleetOrderKind.Attack, 42, 3, 4, true), game.Tables);
        Assert.Equal(new FleetOrderCommand(0, 17, FleetOrderKind.Attack, 42, 3, 4, true), order);

        var route = RoundTrip(new FleetRouteCommand(0, 17, 5, FleetOrderKind.Move, true, [1, 2, 3]), game.Tables);
        Assert.Equal([1, 2, 3], route.SplitOff);

        var trade = RoundTrip(new TradeOfferCommand(0, 1, new TradeTerms([7], [], [9], true, 50), new TradeTerms([], [3], [], false, 0)), game.Tables);
        Assert.Equal(50, trade.Offered.Credits);
        Assert.Equal([3], trade.Asked.Techs);
    }

    [Fact]
    public void AFleetOrderMovesTheFleetByIds()
    {
        var game = NewGameForCommands();
        var human = game.Players[0];
        var fleet = human.Fleets[0];
        var star = game.Universe.Stars[fleet.Star];
        var planet = star.Planets.First(p => p.Colony is null);
        game.Apply(new FleetOrderCommand(0, fleet.ObjectId, FleetOrderKind.Move, planet.ObjectId, 0, 0, true));
        Assert.Same(planet, fleet.Orders.Single().Target);

        game.Apply(new FleetOrderCommand(0, fleet.ObjectId, FleetOrderKind.Move, star.ObjectId, 0, 0, true));
        Assert.Same(star.Center, fleet.Orders.Single().Target);
    }

    [Fact]
    public void APlayerCannotCommandAnothersThings()
    {
        var game = NewGameForCommands();
        var theirs = game.Players[1];
        var fleet = theirs.Fleets[0];
        var hash = game.Hash();
        game.Apply(new FleetOrderCommand(0, fleet.ObjectId, FleetOrderKind.ToggleIntercept, -1, 0, 0, false));
        game.Apply(new AbandonCommand(0, theirs.Colonies[0].Planet.ObjectId));
        game.Apply(new ScrapShipCommand(0, fleet.Ships[0].ShipId));
        game.Apply(new TreatyClickCommand(0, 0, 1));
        game.Apply(new ResearchCommand(99, 1, 0));
        Assert.Equal(hash, game.Hash());
    }

    [Fact]
    public void ADesignSavedFromItsDataMatchesTheDraft()
    {
        var game = NewGameForCommands();
        var human = game.Players[0];
        game.Apply(new AutoDesignCommand(0, 1, "Test"));
        var draft = human.Draft!;
        var free = Array.IndexOf(human.Designs, null);
        game.Apply(new SaveDesignCommand(0, DesignSpec.Of(draft)));
        var saved = human.Designs[free]!;
        Assert.Equal(draft.Name, saved.Name);
        Assert.Equal(draft.Cost, saved.Cost);
        Assert.Equal(draft.UsedSpace, saved.UsedSpace);
        Assert.Equal(DesignSpec.Of(draft).Parts, DesignSpec.Of(saved).Parts);
    }
}
