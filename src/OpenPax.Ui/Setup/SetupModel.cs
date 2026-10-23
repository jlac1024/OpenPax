using System.Collections.ObjectModel;
using OpenPax.Data.Import;
using OpenPax.Data.Install;
using OpenPax.Forms;

namespace OpenPax.Ui.Setup;

/// <summary>
/// The first-run setup (docs/first-run.md, Forms/Markup/Setup.xml): the
/// player points the game at their own copy, a GOG install found on this
/// machine, a folder they pick, or GOG's offline installer, and the game
/// imports from it into the user folder. Nothing of the game ships with it.
/// </summary>
public sealed class SetupModel : Observable
{
    // The import, run by the host on a thread of its own: the source, how far it has got, and the log it writes.
    private readonly Func<string, IProgress<ImportProgress>, TextWriter, bool> _import;
    private readonly IReadOnlyList<string> _installs;
    private Task<bool>? _running;
    // The newest progress the import reported, written by its thread and read
    // each frame: under a lock, as the struct is two fields that must agree.
    // Empty until the import first reports.
    private readonly object _progressGate = new();
    private ImportProgress _latest = new(0, "");
    private StringWriter _log = new();

    /// <param name="galaxyStubFound">GOG's Galaxy stub was seen in the player's downloads: said when nothing else was found.</param>
    public SetupModel(IReadOnlyList<string> installs, Func<string, IProgress<ImportProgress>, TextWriter, bool> import, bool galaxyStubFound = false)
    {
        _installs = installs;
        _import = import;
        Found = [.. installs];
        FoundIndex = installs.Count > 0 ? 0 : -1;
        Path = installs.Count > 0 ? installs[0] : "";
        Status = installs.Count > 0 ? SetupText.Found : galaxyStubFound ? SetupText.OnlyGalaxyStub : SetupText.NoneFound;
    }

    /// <summary>The host should open its folder picker, then give the answer to <see cref="Picked"/>.</summary>
    public event Action? FolderWanted;

    /// <summary>The host should open its file picker for GOG's installer, then give the answer to <see cref="Picked"/>.</summary>
    public event Action? InstallerWanted;

    /// <summary>Set once the import is whole: the host goes on to start the game.</summary>
    public bool Done { get; private set; }

    /// <summary>Set when the player gives up: the host quits.</summary>
    public bool Quit { get; private set; }

    public ObservableCollection<string> Found { get; }

    public int FoundIndex
    {
        get;
        set
        {
            if (Set(ref field, value) && value >= 0 && value < _installs.Count && !Busy)
            {
                Path = _installs[value];
            }
        }
    }

    /// <summary>The game folder, or the installer, to import from.</summary>
    public string Path
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                Changed(nameof(CanImport));
            }
        }
    } = "";

    public string Status
    {
        get;
        private set => Set(ref field, value);
    } = "";

    /// <summary>The import's progress, 0 to 1000.</summary>
    public int Progress
    {
        get;
        private set => Set(ref field, value);
    }

    public bool Busy
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                Changed(nameof(Idle));
                Changed(nameof(CanImport));
            }
        }
    }

    public bool Idle => !Busy;

    public bool CanImport => !Busy && Path.Trim().Length > 0;

    public void OnBrowseFolder()
    {
        if (!Busy)
        {
            FolderWanted?.Invoke();
        }
    }

    public void OnBrowseInstaller()
    {
        if (!Busy)
        {
            InstallerWanted?.Invoke();
        }
    }

    /// <summary>What a picker gave back; null when it was cancelled.</summary>
    public void Picked(string? path)
    {
        if (path is not null && !Busy)
        {
            Path = path;
            Status = IsExe(path) ? InstallerStatus(path) ?? SetupText.Picked : SetupText.Picked;
        }
    }

    public void OnImport()
    {
        if (!CanImport)
        {
            return;
        }

        var source = Path.Trim();
        if (IsExe(source))
        {
            if (InstallerStatus(source) is { } problem)
            {
                Status = problem;
                return;
            }
        }
        else if (GameInstall.Resolve(source) is null)
        {
            Status = SetupText.NotTheGame;
            return;
        }

        Busy = true;
        Progress = 0;
        Status = SetupText.Starting;
        _log = new StringWriter();
        lock (_progressGate)
        {
            // A new try starts from nothing, not from where the last one stopped.
            _latest = new ImportProgress(0, "");
        }

        var progress = new Latest(this);
        var log = TextWriter.Synchronized(_log);
        _running = Task.Run(() => _import(source, progress, log));
    }

    private static bool IsExe(string path) =>
        File.Exists(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    /// <summary>Why a picked .exe cannot be imported, or null when it is GOG's offline installer.</summary>
    private static string? InstallerStatus(string path) => InstallerCheck.Identify(path) switch
    {
        InstallerKind.Offline => null,
        InstallerKind.GalaxyStub => SetupText.GalaxyStub,
        _ => SetupText.NotAnInstaller,
    };

    public void OnQuit()
    {
        if (!Busy)
        {
            Quit = true;
        }
    }

    /// <summary>Each frame: the import's progress shown, and its end taken in.</summary>
    public void Tick()
    {
        if (_running is null)
        {
            return;
        }

        ImportProgress latest;
        lock (_progressGate)
        {
            latest = _latest;
        }

        Progress = (int)(latest.Fraction * 1000);
        if (latest.Step is { Length: > 0 })
        {
            Status = latest.Step + "...";
        }

        if (!_running.IsCompleted)
        {
            return;
        }

        var ok = _running is { IsCompletedSuccessfully: true, Result: true };
        var failure = _running.Exception?.InnerException?.Message;
        _running = null;
        Busy = false;
        if (ok)
        {
            Done = true;
            return;
        }

        // The last thing the import said is why it stopped.
        var said = _log.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Status = SetupText.Failed + (failure ?? said.LastOrDefault() ?? "");
        Progress = 0;
    }

    // Progress from the import's thread: only the newest is kept, for the next frame to show.
    private sealed class Latest(SetupModel model) : IProgress<ImportProgress>
    {
        public void Report(ImportProgress value)
        {
            lock (model._progressGate)
            {
                model._latest = value;
            }
        }
    }
}
