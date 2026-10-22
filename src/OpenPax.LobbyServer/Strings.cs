namespace OpenPax.LobbyServer;

/// <summary>The lobby server's text, in one place.</summary>
internal static class Strings
{
    public const string BadArgument = "openpax-lobby: unknown or malformed argument: {0}. Usage: openpax-lobby [--port N]";
    public const string Listening = "openpax-lobby: listening on port {0}. Ctrl+C stops it.";
    public const string Discoverable = "openpax-lobby: games on the LAN find it as {0} (UDP port {1}); ws:// and wss:// work on the same port.";
    public const string NotDiscoverable = "openpax-lobby: UDP port {0} is taken, so games on the LAN will not find it; they can still type its address.";
    public const string Status = "openpax-lobby: {0} players, {1} rooms";
    public const string Stopped = "openpax-lobby: stopped";
}
