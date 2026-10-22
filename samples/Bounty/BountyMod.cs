using System.Globalization;
using OpenPax.Modding;
using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace Bounty;

/// <summary>
/// The getting started guide's mod (docs/modding/getting-started.md): every
/// enemy ship a player destroys pays credits by its hull, now and then
/// double, and a human is told. The kills are counted in the game's mod
/// state, so they outlast a save.
/// </summary>
public sealed class BountyMod : IMod
{
    private double _perHull = 50;
    private double _doubleChance = 0.1;
    private ModContext? _context;

    public void Load(ModContext context)
    {
        _context = context;
        var settings = context.ReadToml("settings.toml");
        _perHull = Number(settings, "credits_per_hull", _perHull);
        _doubleChance = Number(settings, "double_chance", _doubleChance);
        context.OnShipDestroyed(Paid);
    }

    private void Paid(Game game, Design ship, Player owner, Player? by)
    {
        // A ship lost to no one (a mine, say) or to its own side pays nothing.
        if (by is null || by == owner || _context is not { } context)
        {
            return;
        }

        // The mod's own random numbers: the same in every copy of the game, and saved with it.
        var bonus = context.Random(game).Chance(_doubleChance);
        var credits = _perHull * (ship.Type + 1) * (bonus ? 2 : 1);
        Game.GiveMoney(by, credits);

        var store = context.Store(game);
        var kills = store.GetInt("kills." + by.Index) + 1;
        store.SetInt("kills." + by.Index, kills);
        game.Tell(by, string.Format(CultureInfo.InvariantCulture, bonus ? "Bounty: {0} credits, doubled! ({1} kills)" : "Bounty: {0} credits ({1} kills)", credits, kills));
    }

    private static double Number(IReadOnlyDictionary<string, object> table, string key, double fallback) =>
        table.TryGetValue(key, out var v) ? v switch { long l => l, double d => d, _ => fallback } : fallback;
}
