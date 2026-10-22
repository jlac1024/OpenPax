using OpenPax.Net.Lobby;

namespace OpenPax.Net.Messages;

/// <summary>The game starts: the host gets a ticket for each other member, each member its own; each opens a relay connection with it.</summary>
public sealed record GameStartingMessage(int Host, RelayTicket[] Tickets) : NetMessage;
