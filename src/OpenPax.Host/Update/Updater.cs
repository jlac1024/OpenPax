using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using OpenPax.Ui;

namespace OpenPax.Host.Update;

/// <summary>
/// Checking for and installing updates for the window (docs/updates.md).
/// The network and file work runs off the main thread; each frame
/// <see cref="Follow"/> copies where it stands into the screens' state, and
/// says when the update is in place and the game should restart.
/// </summary>
internal sealed class Updater : IDisposable
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private readonly HttpClient _http;
    private readonly string _folder;
    private readonly object _gate = new();
    private Task<ReleaseInfo?>? _check;
    private Task? _installing;
    private CancellationTokenSource? _cancel;
    private ReleaseInfo? _found;
    private double _progress;
    private string _status = "";
    private bool _installed;
    private bool _checkSaysNewest;

    public Updater(string install, HttpMessageHandler? handler = null)
    {
        _folder = install;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = Timeout.InfiniteTimeSpan;
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OpenPax", AppVersion.Current.ToString()));
    }

    public ReleaseVersion Running { get; init; } = AppVersion.Current;

    /// <summary>Asks GitHub for a newer release, unless asking already. Quiet when it cannot reach GitHub, unless <paramref name="say"/>.</summary>
    public void Check(bool preReleases, bool say)
    {
        if (_check is { IsCompleted: false } || _installing is { IsCompleted: false })
        {
            return;
        }

        _checkSaysNewest = say;
        SetStatus(say ? Strings.UpdateChecking : "");
        _check = Task.Run(async () =>
        {
            using var timeout = new CancellationTokenSource(_timeout);
            var json = await ReleaseFeed.FetchAsync(_http, timeout.Token).ConfigureAwait(false);
            return ReleaseFeed.Newest(json, Running, preReleases, ReleaseFeed.Platform);
        });
    }

    /// <summary>Downloads and swaps in the release found, unless the install folder cannot be changed.</summary>
    public void Install()
    {
        if (_found is not { } release || _installing is { IsCompleted: false })
        {
            return;
        }

        if (InstallFolder.Packaged(_folder) is { } kind)
        {
            // A package's install is the system's to update, from the repositories the package named (docs/platforms.md).
            SetStatus(kind switch
            {
                "Flatpak" => string.Format(CultureInfo.InvariantCulture, Strings.UpdatePackagedFlatpak, release.Version),
                "deb" => string.Format(CultureInfo.InvariantCulture, Strings.UpdatePackagedDeb, release.Version),
                _ => string.Format(CultureInfo.InvariantCulture, Strings.UpdatePackaged, kind, release.Version),
            });
            _installing = Task.CompletedTask;
            return;
        }

        if (!InstallFolder.Writable(_folder))
        {
            // Done at once, with nothing done: the popup closes and says why.
            SetStatus(string.Format(Strings.UpdateNotWritable, release.Page));
            _installing = Task.CompletedTask;
            return;
        }

        _cancel = new CancellationTokenSource();
        var cancel = _cancel.Token;
        _progress = 0;
        SetStatus(string.Format(Strings.UpdateDownloading, release.Version, 0));
        var progress = new DownloadProgress(percent => Downloaded(release, percent));
        _installing = Task.Run(async () =>
        {
            var archive = await UpdateInstaller.DownloadAsync(_http, release, _folder, progress, cancel).ConfigureAwait(false);
            cancel.ThrowIfCancellationRequested();
            SetStatus(string.Format(Strings.UpdateInstalling, release.Version));
            var bundle = UpdateInstaller.Unpack(archive);
            cancel.ThrowIfCancellationRequested();
            UpdateInstaller.Swap(_folder, bundle);
            lock (_gate)
            {
                _installed = true;
            }
        });
    }

    public void Cancel() => _cancel?.Cancel();

    /// <summary>Each frame: where it stands, for the screens. True once the new version is in place and the game should restart.</summary>
    public bool Follow(UpdateState state)
    {
        state.Running = Running.ToString();
        if (_check is { IsCompleted: true } check)
        {
            _check = null;
            if (check.IsCompletedSuccessfully)
            {
                _found = check.Result;
                state.Available = _found?.Version.ToString();
                SetStatus(_found is { } release
                    ? string.Format(release.IsPreRelease ? Strings.UpdatePreReleaseFound : Strings.UpdateFound, release.Version)
                    : _checkSaysNewest ? Strings.UpdateNewest : "");
            }
            else
            {
                SetStatus(_checkSaysNewest ? string.Format(Strings.UpdateCheckFailed, Reason(check.Exception)) : "");
            }
        }

        if (_installing is { IsCompleted: true } install)
        {
            _installing = null;
            if (!install.IsCompletedSuccessfully)
            {
                SetStatus(install.IsCanceled || _cancel?.IsCancellationRequested == true
                    ? Strings.UpdateCancelled
                    : string.Format(Strings.UpdateFailed, Reason(install.Exception)));
                UpdateInstaller.CleanUp(_folder);
            }
        }

        lock (_gate)
        {
            state.Busy = _check is not null || _installing is not null;
            state.Progress = _progress;
            state.Status = _status;
            return _installed;
        }
    }

    /// <summary>Starts the new version, with the arguments this one had; the caller then closes this one.</summary>
    public bool Restart()
    {
        try
        {
            var start = new ProcessStartInfo(InstallFolder.Program(_folder)) { UseShellExecute = false, WorkingDirectory = _folder };
            foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
            {
                start.ArgumentList.Add(arg);
            }

            return Process.Start(start) is not null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException)
        {
            Console.Error.WriteLine(string.Format(Strings.UpdateRestartFailed, e.Message));
            return false;
        }
    }

    public void Dispose()
    {
        _cancel?.Dispose();
        _http.Dispose();
    }

    private void SetStatus(string text)
    {
        lock (_gate)
        {
            _status = text;
        }
    }

    // From the download's thread: the status line follows, a percent at a time.
    private void Downloaded(ReleaseInfo release, int percent)
    {
        lock (_gate)
        {
            _progress = percent / 100.0;
            _status = string.Format(Strings.UpdateDownloading, release.Version, percent);
        }
    }

    private static string Reason(AggregateException? e) =>
        (e?.InnerException is { } inner ? inner : e)?.Message ?? "";
}
