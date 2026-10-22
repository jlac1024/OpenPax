namespace OpenPax.Sim.Commands;

/// <summary>An advisor appointed governor.</summary>
public sealed record AppointGovernorCommand(int Player, int Advisor, int Role) : Command(Player);
