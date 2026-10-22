namespace OpenPax.Sim.Commands;

/// <summary>A treaty box clicked in Foreign Affairs.</summary>
public sealed record TreatyClickCommand(int Player, int Other, int State) : Command(Player);
