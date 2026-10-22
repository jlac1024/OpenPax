namespace OpenPax.Net.Messages;

/// <summary>
/// News about the session rather than the game, from the server to every
/// client: the host changed the speed (Value is the new speed, as the
/// original's speed order 0x14 carries it), a player left (Player is the
/// seat, Value a <see cref="LeaveReason"/>), or a pause vote opened or
/// closed; and from a client, its answer to a pause vote.
/// </summary>
public sealed record SessionNewsMessage(SessionNews Kind, int Player, int Value) : NetMessage;
