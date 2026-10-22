using OpenPax.Sim.Galaxy;

namespace OpenPax.Sim;

/// <summary>
/// A fight a mod runs outside the battle screen (Game.ReportFights): a
/// fleet in it and what the fight is over, the fleet attacked or the planet
/// besieged. A planet's defenders are reported against it too.
/// </summary>
public sealed record ModFight(Players.Fleet Fleet, ISpaceObject Target);
