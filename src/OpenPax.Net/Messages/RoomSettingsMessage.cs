using OpenPax.Net.Lobby;

namespace OpenPax.Net.Messages;

/// <summary>The host changes the room's galaxy; the members see it in the room state.</summary>
public sealed record RoomSettingsMessage(RoomSettings Settings) : NetMessage;
