namespace OpenPax.Sim.Commands;

/// <summary>A colony follows another plan.</summary>
public sealed record ColonyPlanCommand(int Player, int Planet, int Plan) : Command(Player);
