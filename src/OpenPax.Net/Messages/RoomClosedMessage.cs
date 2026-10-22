namespace OpenPax.Net.Messages;

/// <summary>The sender's room is gone.</summary>
public sealed record RoomClosedMessage(string Reason) : NetMessage;
