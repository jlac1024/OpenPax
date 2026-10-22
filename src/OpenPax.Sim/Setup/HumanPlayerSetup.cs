using OpenPax.Sim.Players;

namespace OpenPax.Sim.Setup;

/// <summary>A human player's choices from the new game screens.</summary>
/// <param name="Flag">0 to 29.</param>
/// <param name="FleetColor">0 to 15.</param>
public sealed record HumanPlayerSetup(string Name, int Flag, int FleetColor, Species Species);
