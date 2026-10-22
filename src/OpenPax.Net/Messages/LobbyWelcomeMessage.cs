namespace OpenPax.Net.Messages;

/// <summary>The lobby takes a player in under this member number.</summary>
public sealed record LobbyWelcomeMessage(int Member) : NetMessage;
