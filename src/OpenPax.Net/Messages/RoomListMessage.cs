using OpenPax.Net.Lobby;

namespace OpenPax.Net.Messages;

/// <summary>The rooms open in the lobby.</summary>
public sealed record RoomListMessage(RoomInfo[] Rooms) : NetMessage;
