using OpenPax.Host.Update.DBus;

namespace OpenPax.Host.Update;

/// <summary>
/// Notices a newer version installed outside the game while it runs
/// (docs/updates.md): for a Flatpak, the portal's update monitor says when
/// the installed version is no longer the running one (the software center
/// or flatpak update put it in); for a .deb, the program file in the install
/// folder changes when apt upgrade replaces it. Either way the game can
/// offer to restart into the new version. Other installs update themselves.
/// </summary>
internal sealed class InstallWatch : IDisposable
{
    // How often a .deb's program file is looked at: a stat, cheap.
    private static readonly TimeSpan _debPeriod = TimeSpan.FromSeconds(15);

    private readonly CancellationTokenSource _stop = new();
    private readonly string _install;
    private readonly bool _flatpak;
    private volatile bool _changed;

    public InstallWatch(string install)
    {
        _install = install;
        var kind = InstallFolder.Packaged(install);
        _flatpak = kind == "Flatpak" && FlatpakPortal.Inside;
        if (_flatpak)
        {
            _ = Task.Run(() => WatchFlatpakAsync(_stop.Token));
        }
        else if (kind == "deb")
        {
            _ = Task.Run(() => WatchFileAsync(InstallFolder.Program(install), _stop.Token));
        }
    }

    /// <summary>A newer version is installed than the one running.</summary>
    public bool Changed => _changed;

    /// <summary>Starts the newly installed version, with the arguments this one had; the caller then closes this one.</summary>
    public bool Restart()
    {
        var args = Environment.GetCommandLineArgs().Skip(1);
        if (_flatpak)
        {
            return FlatpakPortal.StartLatest(args);
        }

        var start = new System.Diagnostics.ProcessStartInfo(InstallFolder.Program(_install)) { UseShellExecute = false, WorkingDirectory = _install };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        return System.Diagnostics.Process.Start(start) is not null;
    }

    private async Task WatchFlatpakAsync(CancellationToken cancel)
    {
        try
        {
            await FlatpakPortal.WatchAsync(() => _changed = true, cancel).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Net.Sockets.SocketException)
        {
            // No portal (an old Flatpak, a session without one): nothing is noticed, and the system's updater still works.
            Console.Error.WriteLine(string.Format(Strings.InstallWatchFailed, e.Message));
        }
        catch (OperationCanceledException)
        {
            // The game is closing.
        }
    }

    private async Task WatchFileAsync(string program, CancellationToken cancel)
    {
        try
        {
            var started = File.GetLastWriteTimeUtc(program);
            using var timer = new PeriodicTimer(_debPeriod);
            while (await timer.WaitForNextTickAsync(cancel).ConfigureAwait(false))
            {
                if (File.Exists(program) && File.GetLastWriteTimeUtc(program) != started)
                {
                    _changed = true;
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The game is closing.
        }
        catch (IOException e)
        {
            Console.Error.WriteLine(string.Format(Strings.InstallWatchFailed, e.Message));
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }
}
