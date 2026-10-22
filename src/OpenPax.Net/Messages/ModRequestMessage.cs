namespace OpenPax.Net.Messages;

/// <summary>A mod package asked for by its key ("id@version#hash"): from a member to the lobby, and from the lobby to the room's host.</summary>
public sealed record ModRequestMessage(string Key) : NetMessage;
