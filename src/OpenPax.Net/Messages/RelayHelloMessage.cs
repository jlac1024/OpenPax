namespace OpenPax.Net.Messages;

/// <summary>The first message on a relay connection; after it the lobby carries bytes between the two ends unread.</summary>
public sealed record RelayHelloMessage(string Token, bool Host) : NetMessage;
