namespace OpenPax.Net.Messages;

/// <summary>A line of chat from a player (-1 for the server).</summary>
public sealed record ChatMessage(int From, string Text) : NetMessage;
