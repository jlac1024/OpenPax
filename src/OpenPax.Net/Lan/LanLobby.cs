using System.Globalization;

namespace OpenPax.Net.Lan;

/// <summary>A lobby found on the LAN: its name, where it is, its protocol version and how many players are in it.</summary>
public sealed record LanLobby(string Name, string Host, int Port, int Version, int Players)
{
    /// <summary>The address to connect to, as the connect screen takes it.</summary>
    public string Address => Host + ":" + Port.ToString(CultureInfo.InvariantCulture);
}
