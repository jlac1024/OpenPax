namespace OpenPax.Sim.Commands;

/// <summary>A developers' cheat for the player.</summary>
public sealed record CheatCommand(int Player, Cheat Cheat) : Command(Player);
