using OpenPax.Sim;

namespace OpenPax.Modding;

/// <summary>
/// Random numbers for a mod's rules that keep a game deterministic: a
/// stream of its own (xorshift64*), seeded from the game's seed and the mod's
/// id, its state kept in the mod's store ("id.random"), so it is saved,
/// sent to network clients and hashed like the rest of the game. Never use
/// System.Random or the clock in rules: games would part ways. The game's own
/// stream (Game.Random) is left alone, so the original's rules draw as they did.
/// </summary>
public sealed class ModRandom
{
    private const string Key = "random";
    private readonly ModStore _store;
    private readonly ulong _seed;

    public ModRandom(Game game, string modId)
    {
        _store = new ModStore(game, modId);

        // FNV-1a of the id, mixed with the game's seed; never zero, which xorshift cannot leave.
        var hash = 14695981039346656037UL;
        foreach (var c in modId)
        {
            hash = (hash ^ c) * 1099511628211UL;
        }

        _seed = (hash ^ ((ulong)game.Setup.Seed * 0x9E3779B97F4A7C15UL)) | 1;
    }

    /// <summary>A whole number from 0 to max - 1 (0 when max is 1 or less).</summary>
    public int Next(int max) => max <= 1 ? 0 : (int)(Draw() % (ulong)max);

    /// <summary>A whole number from min to max - 1.</summary>
    public int Next(int min, int max) => min + Next(max - min);

    /// <summary>A number from 0 up to but not including 1.</summary>
    public double NextDouble() => (Draw() >> 11) * (1.0 / (1UL << 53));

    /// <summary>True with the chance given, 0 to 1.</summary>
    public bool Chance(double chance) => NextDouble() < chance;

    private ulong Draw()
    {
        var x = _store.Get(Key) is { } text && ulong.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : _seed;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        _store.Set(Key, x.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return x * 0x2545F4914F6CDD1DUL;
    }
}
