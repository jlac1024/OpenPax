namespace OpenPax.Sim.Setup;

/// <summary>
/// A new game's settings (the original's setup structure, see
/// docs/sim/universe.md). Humans come first in the player order; the rest
/// of <see cref="TotalPlayers"/> are computer players.
/// </summary>
/// <param name="Difficulty">1 (easiest) to 5.</param>
public sealed record GameSetup(uint Seed, int StarCount, int TotalPlayers, int Difficulty, IReadOnlyList<HumanPlayerSetup> Humans);
