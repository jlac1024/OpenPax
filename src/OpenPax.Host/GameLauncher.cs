using OpenPax.Data;
using OpenPax.Data.Tables;
using OpenPax.Host.Headless;
using OpenPax.Sim;
using OpenPax.Sim.Setup;
using OpenPax.Ui;

namespace OpenPax.Host;

/// <summary>Makes a game from the new game screens' choices, loading the game tables the first time.</summary>
internal sealed class GameLauncher(DataFiles files, OpenPax.Modding.ModHost mods)
{
    public GameLauncher(string dataFolder)
        : this(new DataFiles(dataFolder), OpenPax.Modding.ModHost.None)
    {
    }

    /// <summary>The mods games are made and run with (docs/modding.md).</summary>
    public OpenPax.Modding.ModHost Mods => mods;

    private GameTables? _tables;

    /// <summary>The game data, read the first time it is wanted.</summary>
    public GameTables Tables => _tables ??= Load();

    /// <summary>What the mods' content got wrong (docs/modding/content.md), once the tables are read; those entries are left out.</summary>
    public List<string> ContentErrors { get; } = [];

    public Game Start(NewGameChoices choices)
    {
        var human = new HumanPlayerSetup(choices.Name, choices.Flag, choices.FleetColor, GameTablesLoader.ToSpecies(choices.Species));
        var setup = new GameSetup(choices.Seed, choices.Stars, choices.Opponents + 1, choices.Difficulty, [human]);
        var game = NewGame.Create(setup, Tables, new OpenPax.Ai.ComputerPlayer());
        mods.NewGame(game);
        return game;
    }

    /// <summary>A hosted network game: every room member a human, the host first, then the computer opponents.</summary>
    public Game StartNetwork(NetworkChoices choices)
    {
        var humans = choices.Humans.Select(h => new HumanPlayerSetup(h.Name, h.Flag, h.FleetColor, GameTablesLoader.ToSpecies(h.Species))).ToList();
        var setup = new GameSetup(choices.Seed, choices.Stars, choices.Opponents + humans.Count, choices.Difficulty, humans);
        var game = NewGame.Create(setup, Tables, new OpenPax.Ai.ComputerPlayer());
        mods.NewGame(game);
        return game;
    }

    private GameTables Load()
    {
        var directions = DirectionTables.FromToml(File.ReadAllText(files.Path("tables", DirectionTables.FileName)));
        var sprites = SpriteRows.FromToml(File.ReadAllText(files.Path("tables", SpriteRows.FileName)));
        return mods.ChangeTables(GameTablesLoader.Load(files, directions, sprites, ContentErrors));
    }
}
