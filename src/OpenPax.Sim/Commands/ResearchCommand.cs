namespace OpenPax.Sim.Commands;

/// <summary>A tech is researched in a field.</summary>
public sealed record ResearchCommand(int Player, int Tech, int Field) : Command(Player);
