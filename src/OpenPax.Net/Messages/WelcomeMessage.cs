namespace OpenPax.Net.Messages;

/// <summary>The server takes a client in as the player with this index.</summary>
public sealed record WelcomeMessage(int Player) : NetMessage;
