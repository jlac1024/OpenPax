namespace OpenPax.Net.Lobby;

/// <summary>
/// What a member plays as: a stock species by its place in the list, a flag
/// and a fleet color; -1 leaves each to the host's default for the seat. A
/// designed species travels whole as <see cref="Custom"/>, in the original's
/// SpecData layout (one species), and then stands instead of the stock one.
/// </summary>
public sealed record SeatChoice(int Species, int Flag, int Color, byte[]? Custom = null)
{
    /// <summary>The most a designed species may take on the wire; one is a few hundred bytes.</summary>
    public const int MaxCustom = 16 * 1024;

    public static SeatChoice Default { get; } = new(-1, -1, -1);
}
