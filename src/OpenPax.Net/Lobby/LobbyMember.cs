namespace OpenPax.Net.Lobby;

/// <summary>A player connected to the lobby.</summary>
internal sealed class LobbyMember(Connection connection)
{
    public Connection Connection { get; } = connection;

    /// <summary>The member number once the hello is accepted, else 0.</summary>
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Mods { get; set; } = "";

    public Room? Room { get; set; }

    public bool Ready { get; set; }

    public SeatChoice Choice { get; set; } = SeatChoice.Default;
}
