namespace OpenPax.Net.Lobby;

/// <summary>A room: its host first among its members, and the game it starts.</summary>
internal sealed class Room(int id, string name, string password, int maxPlayers, LobbyMember host)
{
    public int Id { get; } = id;

    public string Name { get; } = name;

    public string Password { get; } = password;

    public int MaxPlayers { get; } = maxPlayers;

    public LobbyMember Host { get; } = host;

    public List<LobbyMember> Members { get; } = [host];

    public bool InGame { get; set; }

    public RoomSettings Settings { get; set; } = RoomSettings.Default;

    public RoomInfo Info => new(Id, Name, Host.Name, Members.Count, MaxPlayers, Password.Length > 0, InGame, Host.Mods, Settings);

    public RoomMember[] Roster => [.. Members.Select(m => new RoomMember(m.Id, m.Name, m.Ready, m.Choice) { Mods = m.Mods })];

    /// <summary>Whether every member has the host's mods.</summary>
    public bool ModsMatch => Members.All(m => m.Mods == Host.Mods);
}
