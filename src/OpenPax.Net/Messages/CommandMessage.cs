namespace OpenPax.Net.Messages;

/// <summary>A player's command, in the save format, for the server to apply at the next tick.</summary>
public sealed record CommandMessage(byte[] Command) : NetMessage;
