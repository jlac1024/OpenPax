namespace OpenPax.Net.Lobby;

/// <summary>What a relay connection between the host and one member is opened with.</summary>
public sealed record RelayTicket(int Member, string Token);
