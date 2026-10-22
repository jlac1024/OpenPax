namespace OpenPax.Net;

/// <summary>Numbers both ends of a connection must agree on.</summary>
public static class NetProtocol
{
    /// <summary>Bumped whenever a message changes; a client of another version is refused.</summary>
    public const int Version = 4;

    /// <summary>The game server's TCP port when none is given.</summary>
    public const int DefaultPort = 10420;

    /// <summary>The lobby server's TCP port when none is given; players and relay ends both arrive on it.</summary>
    public const int DefaultLobbyPort = 10421;

    /// <summary>The largest message accepted, so a broken peer cannot ask for any amount of memory.</summary>
    public const int MaxMessage = 64 * 1024 * 1024;
}
