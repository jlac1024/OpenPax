using System.Globalization;
using OpenPax.Sim;

namespace OpenPax.Modding;

/// <summary>
/// A mod's own saved values in a game (<see cref="Game.ModState"/>), under
/// keys of its own: "id.key". They are saved with the game, sent to network
/// clients and part of the state hash, so a value set the same way in every
/// game keeps games in step. Numbers are kept as invariant text that reads
/// back exactly.
/// </summary>
public sealed class ModStore
{
    private readonly Game _game;
    private readonly string _prefix;

    public ModStore(Game game, string modId)
    {
        _game = game;
        _prefix = modId + ".";
    }

    /// <summary>The mod's keys that have a value, in ordinal order, without the mod's prefix.</summary>
    public IReadOnlyList<string> Keys =>
        [.. _game.ModState.Keys.Where(k => k.StartsWith(_prefix, StringComparison.Ordinal)).Select(k => k[_prefix.Length..]).Order(StringComparer.Ordinal)];

    public string? Get(string key) => _game.ModState.TryGetValue(_prefix + key, out var v) ? v : null;

    /// <summary>Sets a value; null removes it.</summary>
    public void Set(string key, string? value)
    {
        if (value is null)
        {
            _game.ModState.Remove(_prefix + key);
        }
        else
        {
            _game.ModState[_prefix + key] = value;
        }
    }

    public void Remove(string key) => _game.ModState.Remove(_prefix + key);

    public long GetLong(string key, long fallback = 0) =>
        long.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public void SetLong(string key, long value) => Set(key, value.ToString(CultureInfo.InvariantCulture));

    public int GetInt(string key, int fallback = 0) =>
        int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public void SetInt(string key, int value) => Set(key, value.ToString(CultureInfo.InvariantCulture));

    public double GetDouble(string key, double fallback = 0) =>
        double.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public void SetDouble(string key, double value) => Set(key, value.ToString("R", CultureInfo.InvariantCulture));

    public bool GetBool(string key, bool fallback = false) => Get(key) switch
    {
        "true" => true,
        "false" => false,
        _ => fallback,
    };

    public void SetBool(string key, bool value) => Set(key, value ? "true" : "false");
}
