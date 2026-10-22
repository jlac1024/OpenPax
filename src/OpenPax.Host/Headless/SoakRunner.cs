using OpenPax.Data.Tables;
using OpenPax.Sim;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Host.Headless;

/// <summary>
/// Plays a computer players only game until it ends or a tick limit,
/// printing how each empire stands now and then. For seeing whether games
/// end, so it skips the per tick hashes a replay keeps.
/// </summary>
internal static class SoakRunner
{
    private const int ReportEvery = 20000;

    public static int Run(string dataFolder, Replay settings, int maxTicks, TextWriter log)
    {
        var directions = DirectionTables.FromToml(File.ReadAllText(Path.Combine(dataFolder, "tables", DirectionTables.FileName)));
        var sprites = SpriteRows.FromToml(File.ReadAllText(Path.Combine(dataFolder, "tables", SpriteRows.FileName)));
        var tables = GameTablesLoader.Load(Path.Combine(dataFolder, "original", "Data"), directions, sprites);
        var setup = new GameSetup(settings.Seed, settings.Stars, settings.Players, settings.Difficulty, []);
        var game = NewGame.Create(setup, tables, new OpenPax.Ai.ComputerPlayer());

        var battles = 0;
        var wasInCombat = false;
        var eliminated = 0;
        while (game.Tick < maxTicks && !game.Over)
        {
            game.Step();
            if (game.InCombat && !wasInCombat)
            {
                battles++;
            }

            wasInCombat = game.InCombat;
            if (game.Players.Count(p => p.Eliminated) is var gone && gone != eliminated)
            {
                eliminated = gone;
                log.WriteLine(string.Format(Strings.SoakEliminated, game.Tick));
            }

            if (game.Tick % ReportEvery == 0 && !game.InCombat)
            {
                Report(game, battles, log);
            }
        }

        Report(game, battles, log);
        log.WriteLine(game.Over ? string.Format(Strings.SoakOver, game.Tick) : string.Format(Strings.SoakNotOver, game.Tick));
        return 0;
    }

    private static void Report(Game game, int battles, TextWriter log)
    {
        var wars = 0;
        var alliances = 0;
        foreach (var a in game.Players.Where(p => !p.Eliminated))
        {
            foreach (var b in game.Players.Where(p => !p.Eliminated && p.Index > a.Index))
            {
                var level = a.Treaties[b.Index].Level();
                wars += level == Treaty.War ? 1 : 0;
                alliances += level is >= Treaty.Allied and not Treaty.None ? 1 : 0;
            }
        }

        log.WriteLine(string.Format(Strings.SoakTick, game.Tick, wars, alliances, battles));
        foreach (var p in game.Players)
        {
            var levels = string.Join(" ", game.Players.Where(q => q != p).Select(q => Level(p.Treaties[q.Index].Level())));
            log.WriteLine(string.Format(Strings.SoakPlayer, p.Index, p.Eliminated ? "out" : "in",
                p.Colonies.Count, p.Fleets.Count, p.Fleets.Sum(f => f.Ships.Count), p.KnownTechs.Count, (long)p.Money, p.Missions.Count, levels));
        }
    }

    private static string Level(int level) => level switch
    {
        Treaty.None => "-",
        Treaty.War => "W",
        _ => level.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
