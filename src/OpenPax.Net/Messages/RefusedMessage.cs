namespace OpenPax.Net.Messages;

/// <summary>The server turns a client away, saying why; the connection then closes.</summary>
public sealed record RefusedMessage(string Reason) : NetMessage;
