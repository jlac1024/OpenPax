namespace OpenPax.Net.Server;

/// <summary>A client at the server: its connection, the player it plays once seated, and the last state it was sent.</summary>
internal sealed class RemotePlayer(Connection connection)
{
    public Connection Connection { get; } = connection;

    /// <summary>The player's index once the hello is accepted, else -1.</summary>
    public int Player { get; set; } = -1;

    /// <summary>The state bytes last sent, which the next update is a difference from; null for a whole state next.</summary>
    public byte[]? LastSent { get; set; }
}
