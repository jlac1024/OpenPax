using System.Globalization;
using OpenPax.Net;

namespace OpenPax.LobbyServer;

/// <summary>The lobby server's command line: <c>--port N</c>.</summary>
public sealed record LobbyOptions(int Port)
{
    /// <summary>The options, or null with a reason when the line is wrong.</summary>
    public static (LobbyOptions? Options, string? Error) Parse(IReadOnlyList<string> args)
    {
        var port = NetProtocol.DefaultLobbyPort;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--port" when i + 1 < args.Count && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var p) && p is > 0 and < 65536:
                    port = p;
                    i++;
                    break;
                default:
                    return (null, string.Format(CultureInfo.InvariantCulture, Strings.BadArgument, args[i]));
            }
        }

        return (new LobbyOptions(port), null);
    }
}
