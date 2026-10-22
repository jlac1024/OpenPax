namespace OpenPax.Net.Messages;

/// <summary>A client's first message: its protocol version, its player's name and its mod set.</summary>
public sealed record HelloMessage(int Protocol, string Name, string Mods) : NetMessage;
