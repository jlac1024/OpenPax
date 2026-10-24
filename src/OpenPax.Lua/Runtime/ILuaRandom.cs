namespace OpenPax.Lua.Runtime;

/// <summary>Where math.random's numbers come from: in a game, the mod's own deterministic stream.</summary>
public interface ILuaRandom
{
    /// <summary>A whole number from 0 to below max (max at least 1).</summary>
    long Next(long max);

    /// <summary>A number from 0 to below 1.</summary>
    double NextDouble();
}
