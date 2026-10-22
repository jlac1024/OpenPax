namespace OpenPax.Net.Messages;

/// <summary>A line of chat: in the room for its members, else for everyone outside rooms. The lobby fills in who said it.</summary>
public sealed record LobbyChatMessage(string From, string Text) : NetMessage;
