namespace OpenPax.Sim;

/// <summary>
/// The game's one random number stream: a linear congruential generator
/// with the classic ANSI C example constants. Every draw the original
/// makes through it must be made here too, in the same order, including
/// draws whose results only affect animation (docs/numerics.md).
/// Owned by the game state; nothing else draws from it.
/// </summary>
public sealed class GameRandom
{
    private uint _state;

    public GameRandom(uint seed)
    {
        _state = seed;
    }

    /// <summary>Current state, for saving and for state hashes.</summary>
    public uint State => _state;

    /// <summary>
    /// Draws made so far. The original counts them too (reset
    /// at game start) though nothing reads the count; tests use it to check
    /// how many draws each step makes.
    /// </summary>
    public long Draws { get; private set; }

    public void Seed(uint seed)
    {
        _state = seed;
    }

    /// <summary>A value from 0 to <paramref name="n"/> - 1.</summary>
    public int Next(int n)
    {
        // The original divides without checking; n == 0 would crash it.
        // No caller passes 0, so it is a programming error here.
        if (n == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(n));
        }

        Draws++;
        _state = unchecked(_state * 1103515245u + 12345u);
        var value = (_state >> 16) & 0x7FFF;

        // Unsigned divide, as in the original: a negative n acts as a huge
        // divisor and returns the value unchanged.
        return (int)(value % (uint)n);
    }
}
