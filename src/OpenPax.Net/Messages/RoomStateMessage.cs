using OpenPax.Net.Lobby;

namespace OpenPax.Net.Messages;

/// <summary>A room as it is now, sent to its members whenever it changes.</summary>
public sealed record RoomStateMessage(RoomInfo Room, RoomMember[] Members) : NetMessage;
