namespace OpenPax.Net.Messages;

/// <summary>A member is ready to start, or no longer.</summary>
public sealed record SetReadyMessage(bool Ready) : NetMessage;
