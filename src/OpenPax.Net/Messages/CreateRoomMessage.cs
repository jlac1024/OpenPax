namespace OpenPax.Net.Messages;

/// <summary>Opens a room with the sender as its host; an empty password leaves it open.</summary>
public sealed record CreateRoomMessage(string Name, string Password, int MaxPlayers) : NetMessage;
