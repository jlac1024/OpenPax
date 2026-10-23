namespace OpenPax.Host;

/// <summary>
/// What the player sees when the game fails: an error box naming the cause
/// and a crash log in the user folder (docs/first-run.md). On Windows the
/// game has no console, so without this a crash closes the window with no
/// word at all. The log also keeps everything the game printed to its
/// console this run, so a report has the whole story.
/// </summary>
internal static class CrashReport
{
    public const string CrashFile = "crash.log";
    public const string LogFile = "openpax.log";

    private static readonly object _gate = new();
    private static string? _folder;
    private static int _reported;

    /// <summary>
    /// From here on, an exception nothing caught, on any thread, is written
    /// to crash.log and shown; and the console's output also goes to
    /// openpax.log in <paramref name="folder"/>, when there is one.
    /// </summary>
    public static void Install(string? folder)
    {
        _folder = folder;
        if (folder is not null)
        {
            try
            {
                var log = new StreamWriter(Path.Combine(folder, LogFile), append: false) { AutoFlush = true };
                Console.SetOut(new TeeWriter(Console.Out, log));
                Console.SetError(new TeeWriter(Console.Error, log));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // No log file: the console alone, as before.
            }
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception);
    }

    /// <summary>Writes the crash log and shows the error. Only the first crash of a run is reported.</summary>
    public static void Report(Exception? error) => Report(error, showBox: true);

    /// <summary>The same; tests pass false, as there is no one to see a box.</summary>
    internal static string? Report(Exception? error, bool showBox)
    {
        if (Interlocked.Exchange(ref _reported, 1) != 0)
        {
            return null;
        }

        var text = string.Join(Environment.NewLine,
            $"OpenPax {typeof(CrashReport).Assembly.GetName().Version}",
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription} ({System.Runtime.InteropServices.RuntimeInformation.OSArchitecture})",
            "",
            error?.ToString() ?? Strings.CrashUnknown);
        string? written = null;
        lock (_gate)
        {
            foreach (var folder in new[] { _folder, Path.GetTempPath() })
            {
                if (folder is null)
                {
                    continue;
                }

                try
                {
                    var path = Path.Combine(folder, CrashFile);
                    File.WriteAllText(path, text);
                    written = path;
                    break;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // The next place, then none.
                }
            }
        }

        try
        {
            Console.Error.WriteLine(text);
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            // Nowhere to print: the file and the box still say it.
        }

        var message = string.Format(Strings.CrashMessage, error?.Message ?? Strings.CrashUnknown, written ?? Strings.CrashNoLog);
        if (showBox)
        {
            try
            {
                Sdl.Sdl.ShowError(Strings.CrashTitle, message);
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
                // SDL is what failed to load: the log still has it.
            }
        }

        return written;
    }

    /// <summary>For tests: where reports go, and a fresh run.</summary>
    internal static void ResetForTests(string? folder)
    {
        _folder = folder;
        _reported = 0;
    }
}
