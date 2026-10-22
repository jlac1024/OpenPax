using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace OpenPax.Ai.Tests;

public class TradeTests
{
    private static void ThinkOnce(Game game, Player p)
    {
        do
        {
            game.Step();
        }
        while (game.LastThinker != p.Index);
    }

    [Fact]
    public void AComputerPlayerTakesAGift()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        var gift = new TradePackage(a, b);
        gift.Offered.Credits = 1000;
        game.OfferPackage(gift);
        Assert.Single(b.Packages);

        var (before, theirs) = (a.Money, b.Money);
        game.AnswerPackage(b.Packages[0], true);

        Assert.Empty(b.Packages);
        Assert.Equal(before - 1000.0, a.Money);
        Assert.Equal(theirs + 1000.0, b.Money);
    }

    [Fact]
    public void ComputerPlayersAnswerWhatWaitsForThem()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        var gift = new TradePackage(a, b);
        gift.Offered.Map = true;
        game.OfferPackage(gift);

        ThinkOnce(game, b);
        Assert.Empty(b.Packages);
    }

    [Fact]
    public void AnAcceptedExchangeMovesTechs()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        var tech = game.Tables.Techs.First(t => !a.Knows(t.Id) && !b.Knows(t.Id));
        game.GiveTech(a, tech);

        var package = new TradePackage(a, b);
        package.Offered.Techs.Add(tech.Id);
        package.Asked.Credits = 10;
        game.OfferPackage(package);
        game.AnswerPackage(package, true);

        Assert.True(b.Knows(tech.Id));
        Assert.Empty(b.Packages);
    }

    [Fact]
    public void APackageThatNoLongerHoldsIsDropped()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        var package = new TradePackage(a, b);
        package.Offered.Credits = (int)a.Money + 1;
        game.OfferPackage(package);

        var theirs = b.Money;
        game.AnswerPackage(package, true);

        Assert.Empty(b.Packages);
        Assert.Equal(theirs, b.Money);
    }

    [Fact]
    public void ARejectionCostsGoodwillBothWays()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        var package = new TradePackage(a, b);
        package.Asked.Credits = 100;
        game.OfferPackage(package);

        var (ab, ba) = (a.RelationScores[b.Index], b.RelationScores[a.Index]);
        game.AnswerPackage(package, false);

        Assert.Equal(ab - 5.0, a.RelationScores[b.Index]);
        Assert.Equal(ba - 5.0, b.RelationScores[a.Index]);
    }

    [Fact]
    public void ADemandWearsPatienceDown()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        var demand = new TradePackage(a, b);
        demand.Asked.Credits = 100;
        game.OfferPackage(demand);

        ThinkOnce(game, b);
        Assert.Equal(20.0, b.Patience[a.Index]);
        Assert.Empty(b.Packages);
    }

    [Fact]
    public void AColonyChangesHandsWithItsPeople()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        var colony = a.Colonies.Single();
        var people = colony.CurrentPopulation;

        var package = new TradePackage(a, b);
        package.Offered.Planets.Add(colony.Planet.ObjectId);
        game.OfferPackage(package);
        game.AnswerPackage(package, true);

        Assert.Equal(b, colony.Planet.Owner);

        // Up to what the planet holds for the new owner's species.
        var colonized = b.Colonies[0];
        Assert.Equal(colony.Planet, colonized.Planet);
        Assert.Equal(Math.Min(people, colonized.MaxPopulation), colonized.CurrentPopulation);
        Assert.DoesNotContain(colony, a.Colonies);
    }

    [Fact]
    public void ShipsGivenToAComputerPlayerAreLost()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        var ship = a.Fleets.SelectMany(f => f.Ships).First();
        var fleets = b.Fleets.Count;

        var package = new TradePackage(a, b);
        package.Offered.Ships.Add(ship.ShipId);
        game.OfferPackage(package);
        game.AnswerPackage(package, true);

        Assert.Null(ship.Fleet);
        Assert.Equal(fleets + 1, b.Fleets.Count);
        Assert.Empty(b.Fleets[0].Ships);
        Assert.Equal(game.Tables.TradedFleetName, b.Fleets[0].Name);
    }

    [Fact]
    public void OnlyTheItemListsTellPackagesApart()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        var first = new TradePackage(a, b);
        first.Offered.Credits = 5;
        var second = new TradePackage(a, b);
        second.Offered.Credits = 500;

        game.OfferPackage(first);
        game.OfferPackage(second);
        game.AnswerPackage(first, false);

        // The newest, second, is matched first and goes instead.
        Assert.Equal(5, b.Packages.Single().Offered.Credits);
    }
}
