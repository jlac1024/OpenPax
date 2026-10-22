namespace OpenPax.Sim;

/// <summary>A colony's fighting strength as a mod rates it (Game.RateColony): what its defenses deal a round, and what they can take.</summary>
public sealed record ColonyRating(int Attack, int Defense);
