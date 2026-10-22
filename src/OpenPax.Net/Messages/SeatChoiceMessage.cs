using OpenPax.Net.Lobby;

namespace OpenPax.Net.Messages;

/// <summary>A member chooses what to play as; the room's state carries it to everyone.</summary>
public sealed record SeatChoiceMessage(SeatChoice Choice) : NetMessage;
