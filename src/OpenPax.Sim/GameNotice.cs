using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// Something the game tells a human player: the original calls a player
/// virtual during the tick and the screens make the text. Computer players
/// get none. Not part of the game's state: the screens take them away.
/// </summary>
/// <param name="Value">A tech id, or an espionage mission's kind.</param>
/// <param name="Other">The other player involved.</param>
/// <param name="Place">The planet or yard planet involved.</param>
/// <param name="Count">How many things a mission hit; for a failed mission 0 found nothing, 1 was seen, 2 failed unseen.</param>
/// <param name="Named">For espionage damage: whether the spy's owner is named.</param>
/// <param name="Text">For a mod's message: its words.</param>
public sealed record GameNotice(
    NoticeKind Kind,
    Player Recipient,
    int Value = 0,
    Player? Other = null,
    ISpaceObject? Place = null,
    Design? Ship = null,
    int Count = 0,
    bool Named = false,
    string? Text = null);
