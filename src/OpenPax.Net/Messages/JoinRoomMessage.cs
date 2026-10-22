namespace OpenPax.Net.Messages;

/// <summary>Asks to join a room.</summary>
public sealed record JoinRoomMessage(int Room, string Password) : NetMessage;
