namespace OpenPax.Sim.Commands;

/// <summary>A treaty offer answered.</summary>
public sealed record TreatyReplyCommand(int Player, int Other, int State, int Value) : Command(Player);
