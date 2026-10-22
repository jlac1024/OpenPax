namespace OpenPax.Net.Lobby;

/// <summary>A member of a room: lobby member number, name, whether ready, and what they play as. The host is the first.</summary>
public sealed record RoomMember(int Member, string Name, bool Ready, SeatChoice Choice)
{
    /// <summary>The member's mods now (mod keys in order); a member plays once they are the host's (docs/modding/multiplayer.md).</summary>
    public string Mods { get; init; } = "";
}
