using OpenPax.Sim.Galaxy;

namespace OpenPax.Sim.Setup;

/// <summary>
/// Starts a game exactly as the original does:
/// players, relations, galaxy, then each player's home and starting assets.
/// </summary>
public static class NewGame
{
    /// <param name="computerPlayers">The AI modules (OpenPax.Ai); without them computer players only get their turn picked.</param>
    public static Game Create(GameSetup setup, GameTables tables, IComputerPlayer? computerPlayers = null)
    {
        if (setup.TotalPlayers < 1 || setup.TotalPlayers > Players.Player.MaxPlayers || setup.Humans.Count > setup.TotalPlayers)
        {
            throw new ArgumentOutOfRangeException(nameof(setup));
        }

        if (setup.Difficulty is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(setup));
        }

        var game = new Game(setup, tables, new GameRandom(setup.Seed)) { ComputerPlayers = computerPlayers };

        game.Random.Seed(setup.Seed);
        PlayerCreation.CreatePlayers(game, tables);
        Relations.Set(game);

        // The galaxy reseeds, so it is the same for a seed and star count
        // whoever plays.
        game.Universe = UniverseGenerator.Generate(setup.Seed, setup.StarCount, tables.GalaxyNames, game.Random, game.ObjectIds, tables.PlanetKinds);

        // each player's home, then its assets, in turn.
        var free = Enumerable.Repeat(true, setup.StarCount).ToArray();
        var homes = new List<int>();
        foreach (var player in game.Players)
        {
            var starIndex = Homeworlds.ChooseHomeStar(game, free, homes);
            homes.Add(starIndex);
            var star = game.Universe.Stars[starIndex];
            var planet = Homeworlds.ChooseHomePlanet(star, player.Species);
            player.HomeStar = starIndex;
            player.HomePlanetSlot = planet.Slot;
            Homeworlds.MakeHomeworld(game, player, planet);
            StartingAssets.Give(game, player, star, planet, tables);
        }

        foreach (var player in game.Players)
        {
            game.GivePremadeDesigns(player);
        }

        return game;
    }
}
