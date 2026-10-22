using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;
using OpenPax.Sim.Time;

namespace OpenPax.Sim;

/// <summary>A game in progress: the whole simulation state.</summary>
public sealed partial class Game
{
    internal Game(GameSetup setup, GameTables tables, GameRandom random)
    {
        Scheduler = Scheduler.ForNewGame(setup.Difficulty, networked: false);
        Setup = setup;
        Tables = tables;
        Random = random;
    }

    [ModApi]
    public GameSetup Setup { get; }

    /// <summary>The game data this game was started with.</summary>
    [ModApi]
    public GameTables Tables { get; }

    /// <summary>The one game stream (docs/numerics.md).</summary>
    public GameRandom Random { get; }

    /// <summary>Ids of objects in the universe: stars, planets, wormholes, fleets.</summary>
    public ObjectIds ObjectIds { get; } = new();

    /// <summary>Ids of advisors, designs and ships.</summary>
    public ObjectIds SecondaryIds { get; } = new();

    /// <summary>The next espionage mission's number (kept in saved games).</summary>
    public int NextMissionId { get; internal set; }

    /// <summary>The fixes to the original's bugs this game plays with (<see cref="Sim.Fixes"/>); none plays as the original.</summary>
    [ModApi]
    public Fixes Fixes { get; set; }

    /// <summary>Whether this game plays with a fix.</summary>
    [ModApi]
    public bool Fixed(Fixes fix) => (Fixes & fix) != 0;

    [ModApi]
    public Universe Universe { get; internal set; } = new([]);

    [ModApi]
    public List<Player> Players { get; } = [];

    /// <summary>The computer player modules, run by the AI task.</summary>
    public IComputerPlayer? ComputerPlayers { get; internal init; }

    /// <summary>Ticks run since the game started.</summary>
    [ModApi]
    public long Tick { get; internal set; }

    public Calendar Calendar { get; } = new();

    /// <summary>Single player for now; networked games drop the autosave task.</summary>
    public Scheduler Scheduler { get; }

    /// <summary>Where the computer player task looks for the next player to think (task +0x10).</summary>
    public int ComputerPlayerCursor { get; private set; }

    /// <summary>The player the computer player task picked last, or -1. Kept for tests and the replay log.</summary>
    public int LastThinker { get; private set; } = -1;

    /// <summary>The mods this game plays with, as "id@version" in their order (docs/modding.md); empty for none.</summary>
    [ModApi]
    public List<string> Mods { get; } = [];

    /// <summary>
    /// After each step, before its hash is taken: where a mod's rules act
    /// (docs/modding.md). Not saved; whoever runs the game (single player, or
    /// the server) attaches the mods' handlers again on loading.
    /// </summary>
    [ModApi]
    [field: Saving.NotSaved]
    public event Action<Game>? Stepped;

    /// <summary>Runs one tick of the strategic game.</summary>
    public StepEvents Step()
    {
        var events = StepOnce();
        Stepped?.Invoke(this);
        Recording?.Hashes.Add(Hash());
        return events;
    }

    private StepEvents StepOnce()
    {
        // While battles are waiting, strategic time stands still ( combat mode).
        if (InCombat)
        {
            return CombatStep();
        }

        var events = StepEvents.None;
        Tick++;
        Scheduler.Tick(kind => RunTask(kind, ref events));
        return events;
    }

    public ulong Hash()
    {
        var hash = new StateHash();
        hash.Add(Setup.Seed).Add(Setup.Difficulty).Add(Random.State).Add(ObjectIds.Next).Add(SecondaryIds.Next).Add(NextMissionId);
        hash.Add(Battles.Count);
        hash.Add(Tick).Add(ComputerPlayerCursor).Add(Over ? 1 : 0);

        // Only a game with fixes or mods hashes them, so games as the original keep their hashes.
        if (Fixes != Fixes.None)
        {
            hash.Add((int)Fixes);
        }

        foreach (var mod in Mods)
        {
            hash.Add(mod);
        }

        foreach (var (key, value) in ModState.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            hash.Add(key).Add(value);
        }
        Calendar.AddTo(hash);
        Scheduler.AddTo(hash);
        Universe.AddTo(hash);

        // Fleets by system are game state, not galaxy, so the galaxy's own hash stays put.
        foreach (var star in Universe.Stars)
        {
            hash.Add(star.Fleets.Count);
            foreach (var f in star.Fleets)
            {
                hash.Add(f.ObjectId);
            }

            foreach (var w in star.Wormholes)
            {
                hash.Add(w.Known ? 1 : 0);
            }

            foreach (var sight in star.Sight)
            {
                hash.Add(sight);
            }
        }

        hash.Add(Players.Count);
        foreach (var p in Players)
        {
            p.AddTo(hash);
        }

        return hash.Value;
    }
}
