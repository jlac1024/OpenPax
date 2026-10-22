namespace OpenPax.Ui;

/// <summary>A network game to make: the galaxy the room's host set, and the humans in seat order, the host first.</summary>
public sealed record NetworkChoices(uint Seed, int Stars, int Opponents, int Difficulty, IReadOnlyList<NetworkSeat> Humans);
