namespace OpenPax.Net.Messages;

/// <summary>A player arriving at the lobby: protocol version, name and mod set.</summary>
public sealed record LobbyHelloMessage(int Protocol, string Name, string Mods) : NetMessage;
