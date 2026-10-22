namespace OpenPax.Net.Messages;

/// <summary>A client whose state no longer matches asks for the whole game again.</summary>
public sealed record SnapshotRequestMessage : NetMessage;
