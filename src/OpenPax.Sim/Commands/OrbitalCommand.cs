namespace OpenPax.Sim.Commands;

/// <summary>An orbital structure ordered at a colony.</summary>
public sealed record OrbitalCommand(int Player, int Planet, int Tech) : Command(Player);
