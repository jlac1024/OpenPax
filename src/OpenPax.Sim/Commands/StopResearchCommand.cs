namespace OpenPax.Sim.Commands;

/// <summary>A field stops its research.</summary>
public sealed record StopResearchCommand(int Player, int Field) : Command(Player);
