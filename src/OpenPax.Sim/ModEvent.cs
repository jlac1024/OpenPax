using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// Something that happened in the game, for mods (<see cref="Game.Happened"/>,
/// ModContext.OnEvent). Raised for every player, computer players too, at the
/// moment it happens inside a step, so a handler sees the game mid step.
/// </summary>
/// <param name="Player">Whose it is: who learned, built, lost the ship, founded, received, offered the treaty or was eliminated.</param>
/// <param name="Other">The other player, when there is one.</param>
/// <param name="Place">The planet, when there is one.</param>
/// <param name="State">For a treaty: the slot (Treaty.War to Treaty.ShareInformation, or Treaty.None for all).</param>
/// <param name="Value">For a treaty: the slot's new value (Treaty.Off, InForce, OfferedByMe, OfferedToMe).</param>
public sealed record ModEvent(
    ModEventKind Kind,
    Player Player,
    Player? Other = null,
    ISpaceObject? Place = null,
    Design? Ship = null,
    TechInfo? Tech = null,
    int State = 0,
    int Value = 0);
