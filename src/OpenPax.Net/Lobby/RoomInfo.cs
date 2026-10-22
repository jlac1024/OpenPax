namespace OpenPax.Net.Lobby;

/// <summary>A room as the room list shows it, with the galaxy its host has set.</summary>
public sealed record RoomInfo(int Id, string Name, string Host, int Players, int MaxPlayers, bool Locked, bool InGame, string Mods, RoomSettings Settings);
