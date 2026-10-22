using System.Diagnostics;
using System.Globalization;
using OpenPax.LobbyServer;
using OpenPax.Net.Lobby;

var (options, error) = LobbyOptions.Parse(args);
if (options is null)
{
    Console.Error.WriteLine(error);
    return 2;
}

using var lobby = new LobbyServer();
var port = lobby.Listen(options.Port);
Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.Listening, port));
Console.WriteLine(lobby.Discoverable
    ? string.Format(CultureInfo.InvariantCulture, Strings.Discoverable, lobby.Name, port)
    : string.Format(CultureInfo.InvariantCulture, Strings.NotDiscoverable, port));

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.Cancel();
};

// The lobby is light work: a pass every 10 ms, and a status line when the numbers change.
var clock = Stopwatch.StartNew();
var last = (-1, -1);
while (!stop.IsCancellationRequested)
{
    lobby.Tick(clock.ElapsedMilliseconds);
    var now = (lobby.MemberCount, lobby.RoomCount);
    if (now != last)
    {
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.Status, now.MemberCount, now.RoomCount));
        last = now;
    }

    Thread.Sleep(10);
}

Console.WriteLine(Strings.Stopped);
return 0;
