namespace OpenPax.Sim.Commands;

/// <summary>The end window's Observe Game: every star seen.</summary>
public sealed record ObserveCommand(int Player) : Command(Player);
