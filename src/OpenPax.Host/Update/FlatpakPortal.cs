using System.Diagnostics;
using OpenPax.Host.Update.DBus;

namespace OpenPax.Host.Update;

/// <summary>
/// A Flatpak updating itself (docs/updates.md): the Flatpak portal, which
/// any sandboxed app may ask to install its own newer version from the
/// repository it came from, without leaving the sandbox. One D-Bus
/// connection for the whole of it, as the portal keeps an update monitor
/// only while the connection that made it is open: the monitor is made,
/// Update asked for, and its Progress signals followed to the end.
/// </summary>
internal static class FlatpakPortal
{
    private const string Portal = "org.freedesktop.portal.Flatpak";
    private const string PortalPath = "/org/freedesktop/portal/Flatpak";
    private const string Monitor = "org.freedesktop.portal.Flatpak.UpdateMonitor";

    // The portal's status codes in a Progress signal.
    private const long Running = 0;
    private const long NothingNew = 1;
    private const long Done = 2;
    private const long Failed = 3;

    /// <summary>Whether the game runs in a Flatpak sandbox (every one has this file).</summary>
    public static bool Inside => File.Exists("/.flatpak-info");

    /// <summary>
    /// Has the portal install the newest version of this app, reporting
    /// progress from 0 to 100. Done when it is in place; an exception says
    /// why not (nothing newer, a failure, or no portal).
    /// </summary>
    public static async Task UpdateAsync(Action<int> progress, CancellationToken cancel)
    {
        using var bus = await DBusConnection.OpenSessionAsync(cancel).ConfigureAwait(false);
        await bus.CallAsync("org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus", "AddMatch", cancel, "type='signal',interface='" + Monitor + "'").ConfigureAwait(false);
        var created = await bus.CallAsync(Portal, PortalPath, Portal, "CreateUpdateMonitor", cancel, [null]).ConfigureAwait(false);
        var monitor = created.FirstOrDefault() as string ?? throw new IOException(Strings.FlatpakNoMonitor);
        try
        {
            await bus.CallAsync(Portal, monitor, Monitor, "Update", cancel, "", null).ConfigureAwait(false);
            while (true)
            {
                var signal = await bus.NextSignalAsync(cancel).ConfigureAwait(false);
                if (signal.Member != "Progress" || signal.Path != monitor || signal.Body.FirstOrDefault() is not Dictionary<string, object> info)
                {
                    continue;
                }

                var status = info.TryGetValue("status", out var s) && s is long st ? st : Running;
                if (info.TryGetValue("progress", out var p) && p is long percent)
                {
                    progress((int)Math.Clamp(percent, 0, 100));
                }

                switch (status)
                {
                    case Done:
                        progress(100);
                        return;
                    case NothingNew:
                        throw new IOException(Strings.FlatpakNothingNew);
                    case Failed:
                        throw new IOException(info.TryGetValue("error_message", out var m) && m is string message && message.Length > 0 ? message : Strings.FlatpakFailed);
                }
            }
        }
        finally
        {
            // The monitor goes with the connection anyway; closing it says so plainly.
            try
            {
                using var close = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await bus.CallAsync(Portal, monitor, Monitor, "Close", close.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException)
            {
                Console.Error.WriteLine(e.Message);
            }
        }
    }

    /// <summary>
    /// Follows the portal's UpdateAvailable signals on a monitor of its own,
    /// calling back once the installed version is not the running one (an
    /// update put in from outside: the software center, flatpak update). The
    /// portal looks at the installation and the repository now and then.
    /// </summary>
    public static async Task WatchAsync(Action installed, CancellationToken cancel)
    {
        using var bus = await DBusConnection.OpenSessionAsync(cancel).ConfigureAwait(false);
        await bus.CallAsync("org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus", "AddMatch", cancel, "type='signal',interface='" + Monitor + "'").ConfigureAwait(false);
        var created = await bus.CallAsync(Portal, PortalPath, Portal, "CreateUpdateMonitor", cancel, [null]).ConfigureAwait(false);
        var monitor = created.FirstOrDefault() as string ?? throw new IOException(Strings.FlatpakNoMonitor);
        while (true)
        {
            var signal = await bus.NextSignalAsync(cancel).ConfigureAwait(false);
            if (signal.Member == "UpdateAvailable" && signal.Path == monitor && signal.Body.FirstOrDefault() is Dictionary<string, object> info
                && info.TryGetValue("running-commit", out var running) && info.TryGetValue("local-commit", out var local)
                && running is string r && local is string l && l.Length > 0 && l != r)
            {
                installed();
                return;
            }
        }
    }

    /// <summary>Starts the newest installed version of this app, in its own new sandbox, with these arguments.</summary>
    public static bool StartLatest(IEnumerable<string> args)
    {
        var start = new ProcessStartInfo("flatpak-spawn") { UseShellExecute = false };
        start.ArgumentList.Add("--latest-version");
        start.ArgumentList.Add("/app/bin/openpax");
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        return Process.Start(start) is not null;
    }
}
