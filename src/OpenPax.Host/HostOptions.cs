using System.Globalization;
using OpenPax.Ui;

namespace OpenPax.Host;

/// <summary>Command line options for the game executable.</summary>
internal sealed record HostOptions
{
    public int? ExitAfterMilliseconds { get; private set; }

    public string? ImportFrom { get; private set; }

    public string? ImportTo { get; private set; }

    /// <summary>A data folder whose 2x art is remade alone, for trying upscaler changes without a whole import.</summary>
    public string? UpscaleData { get; private set; }

    public string? Ffmpeg { get; private set; }

    public string? Screenshot { get; private set; }

    /// <summary>Movies the game asks for are skipped; for scripted runs only.</summary>
    public bool NoMovies { get; private set; }

    /// <summary>Scripted clicks and key commands, in order.</summary>
    public List<ScriptStep> Script { get; } = [];

    /// <summary>Ticks for a headless computer players game; null for a normal start.</summary>
    public int? HeadlessTicks { get; private set; }

    public uint Seed { get; private set; } = 1234;

    public int Stars { get; private set; } = 30;

    public int Players { get; private set; } = 4;

    public int Difficulty { get; private set; } = 3;

    public string ReplayOut { get; private set; } = "replay.toml";

    /// <summary>--replay-out was given: a dedicated server writes its game's replay there.</summary>
    public bool ReplayOutGiven { get; private set; }

    public string? VerifyReplay { get; private set; }

    /// <summary>Tick limit for a computer players game played to its end; null for none.</summary>
    public int? SoakTicks { get; private set; }

    public bool IsImport => ImportFrom is not null;

    /// <summary>A lobby's address: run a dedicated game server there instead of the game (<c>--dedicated host:port</c>).</summary>
    public string? Dedicated { get; private set; }

    /// <summary>The dedicated server's name in the lobby.</summary>
    public string ServerName { get; private set; } = "OpenPax Server";

    /// <summary>The dedicated server's room name; its name with "'s game" when none.</summary>
    public string? RoomName { get; private set; }

    public string RoomPassword { get; private set; } = "";

    public int Opponents { get; private set; } = 2;

    /// <summary>How many players must be in the room and ready before the dedicated server starts.</summary>
    public int MinPlayers { get; private set; } = 2;

    /// <summary>The mods a dedicated server runs with, in order (<c>--mods a,b</c>), from the mods folder in the user folder.</summary>
    public IReadOnlyList<string> Mods { get; private set; } = [];

    /// <summary>
    /// Parses the command line.
    /// <c>--exit-after-ms N</c> closes the window after N milliseconds; build
    /// and smoke checks use it to run without a person.
    /// <c>--import GAME OUT</c> verifies the GOG install in GAME, converts it
    /// into OUT and exits without opening a window. build.sh uses it.
    /// <c>--ffmpeg PATH</c> gives the import the ffmpeg tool for the movies.
    /// <c>--screenshot PATH</c> saves the first drawn screen as a PNG and exits.
    /// <c>--click X,Y</c> (repeatable) clicks at a screen position before that first screen.
    /// <c>--key NAME</c> (repeatable, in order with the clicks) gives a key command: F1 to F12, Escape, or Ctrl+F1 to Ctrl+F3.
    /// <c>--no-movies</c> skips every movie, so scripted runs are not held up; a development aid.
    /// <c>--headless TICKS</c> plays a computer players only game without a
    /// window, set up by <c>--seed</c>, <c>--stars</c>, <c>--players</c> and
    /// <c>--difficulty</c>, and writes a replay to <c>--replay-out PATH</c>.
    /// <c>--verify-replay PATH</c> replays one and checks every tick's hash.
    /// <c>--soak TICKS</c> plays such a game until it ends or TICKS, printing how it goes.
    /// </summary>
    public static HostOptions? Parse(string[] args, TextWriter errors)
    {
        var o = new HostOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            var ok = (args[i], value) switch
            {
                ("--exit-after-ms", { } v) when Number(v) is { } ms => Set(() => o.ExitAfterMilliseconds = ms),
                ("--import", { } v) when i + 2 < args.Length => Set(() => (o.ImportFrom, o.ImportTo) = (v, args[i + 2])),
                ("--ffmpeg", { } v) => Set(() => o.Ffmpeg = v),
                ("--upscale", { } v) => Set(() => o.UpscaleData = v),
                ("--screenshot", { } v) => Set(() => o.Screenshot = v),
                ("--click", { } v) when ParsePoint(v) is { } point => Set(() => o.Script.Add(new ScriptStep(point.X, point.Y, null))),
                ("--key", { } v) when ParseKey(v) is { } command => Set(() => o.Script.Add(new ScriptStep(0, 0, command))),
                ("--headless", { } v) when Number(v) is { } ticks => Set(() => o.HeadlessTicks = ticks),
                ("--seed", { } v) when Number(v) is { } seed => Set(() => o.Seed = (uint)seed),
                ("--stars", { } v) when Number(v) is { } stars => Set(() => o.Stars = stars),
                ("--players", { } v) when Number(v) is { } players => Set(() => o.Players = players),
                ("--difficulty", { } v) when Number(v) is { } difficulty => Set(() => o.Difficulty = difficulty),
                ("--replay-out", { } v) => Set(() => (o.ReplayOut, o.ReplayOutGiven) = (v, true)),
                ("--verify-replay", { } v) => Set(() => o.VerifyReplay = v),
                ("--soak", { } v) when Number(v) is { } ticks => Set(() => o.SoakTicks = ticks),
                ("--no-movies", _) => Set(() => o.NoMovies = true),
                ("--dedicated", { } v) => Set(() => o.Dedicated = v),
                ("--name", { } v) => Set(() => o.ServerName = v),
                ("--mods", { } v) => Set(() => o.Mods = [.. v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]),
                ("--room", { } v) => Set(() => o.RoomName = v),
                ("--password", { } v) => Set(() => o.RoomPassword = v),
                ("--opponents", { } v) when Number(v) is { } n => Set(() => o.Opponents = n),
                ("--min-players", { } v) when Number(v) is { } n && n >= 1 => Set(() => o.MinPlayers = n),
                _ => false,
            };
            if (!ok)
            {
                errors.WriteLine(string.Format(Strings.BadArgument, args[i]));
                return null;
            }

            // Every option takes one value except --import (two) and --no-movies (none).
            i += args[i] switch
            {
                "--import" => 2,
                "--no-movies" => 0,
                _ => 1,
            };
        }

        return o;
    }

    private static bool Set(Action apply)
    {
        apply();
        return true;
    }

    private static int? Number(string text)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    /// <summary>F1 to F12 by name, as the accelerator table maps them.</summary>
    private static UiCommand? ParseKey(string text)
    {
        if (text == "Escape")
        {
            return KeyMap.ToCommand(Sdl.Sdl.KeyEscape, 0);
        }

        if (text.StartsWith("Ctrl+", StringComparison.Ordinal) && ParseKey(text[5..]) is not null)
        {
            var key = int.Parse(text.AsSpan(6), System.Globalization.CultureInfo.InvariantCulture);
            return KeyMap.ToCommand(Sdl.Sdl.KeyF1 + (uint)(key - 1), Sdl.Sdl.ModCtrl);
        }

        return text.Length is 2 or 3 && text[0] == 'F' && int.TryParse(text[1..], System.Globalization.CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 12
            ? KeyMap.ToCommand(Sdl.Sdl.KeyF1 + (uint)(n - 1), 0)
            : null;
    }

    private static (int X, int Y)? ParsePoint(string text)
    {
        var parts = text.Split(',');
        return parts.Length == 2 && Number(parts[0]) is { } x && Number(parts[1]) is { } y ? (x, y) : null;
    }
}
