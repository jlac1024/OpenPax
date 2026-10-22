using System.Globalization;
using OpenPax.Render;
using OpenPax.Sim;
using OpenPax.Sim.Commands;
using OpenPax.Ui.Screens;

namespace OpenPax.Ui;

/// <summary>
/// The port's developer console, not in the original: opened and closed
/// with the ` key. Typing "cheats on" unlocks the developers' cheats of the
/// original's unreachable cheat window (docs/ui/developer-window.md), each a
/// command here, and the window itself.
/// </summary>
public sealed class DevConsole(UiApp app)
{
    private const int Lines = 12;
    private const int LineHeight = 12;
    private const int MaxInput = 60;
    private const byte Back = 0;
    private const byte Ink = 255;
    private const byte Prompt = 22;

    private static readonly Dictionary<string, Cheat> _cheats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["money"] = Cheat.GiveLotsOfMoney,
        ["buildall"] = Cheat.BuildAll,
        ["kill"] = Cheat.KillPlayer,
        ["research"] = Cheat.ResearchAll,
        ["spies"] = Cheat.EspionageSucceeded,
        ["ships"] = Cheat.MoreShips,
        ["colonize"] = Cheat.ColonizeSectors,
        ["explore"] = Cheat.ExploreAll,
        ["planetcombat"] = Cheat.PlanetCombat,
        ["fleetcombat"] = Cheat.FleetCombat,
        ["synctest"] = Cheat.SpecialTest,
    };

    private readonly List<string> _output = [UiText.ConsoleWelcome];

    public bool Open { get; private set; }

    /// <summary>Cheats are off until "cheats on" is typed; they stay as set for the run.</summary>
    public bool CheatsOn { get; internal set; }

    public string Input { get; private set; } = "";

    public IReadOnlyList<string> Output => _output;

    public void Toggle()
    {
        Open = !Open;
    }

    public void Close()
    {
        Open = false;
    }

    public void Key(UiKey key)
    {
        switch (key)
        {
            case UiKey.Backspace when Input.Length > 0:
                Input = Input[..^1];
                break;
            case UiKey.Return:
                var line = Input.Trim();
                Input = "";
                if (line.Length > 0)
                {
                    Say("> " + line);
                    Run(line);
                }

                break;
        }
    }

    public void Typed(char c)
    {
        // The key that opens the console also arrives as text.
        if (c != '`' && !char.IsControl(c) && Input.Length < MaxInput)
        {
            Input += c;
        }
    }

    /// <summary>Runs one command line.</summary>
    public void Run(string line)
    {
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = words[0].ToLowerInvariant();
        var session = app.Session;
        switch (command)
        {
            case "art" when words.Length > 1 && words[1] is "hd" or "classic":
                // The port's own look switches, for comparing side by side.
                app.Preferences.Set(Preferences.UpscaledArt, words[1] == "hd" ? 1 : 0);
                Say(UiText.ConsoleSet);
                return;
            case "font" when words.Length > 1 && words[1] is "original" or "upscaled" or "modern":
                app.Preferences.Set(Preferences.TextFont, words[1] == "original" ? 0 : words[1] == "upscaled" ? 1 : 2);
                Say(UiText.ConsoleSet);
                return;
            case "outline" when words.Length > 1 && words[1] is "off" or "colors" or "colours" or "white":
                app.Preferences.Set(Preferences.TextOutline, words[1] == "off" ? 0 : words[1] is "colors" or "colours" ? 1 : 2);
                Say(UiText.ConsoleSet);
                return;
            case "fps":
                Say(app.FrameMs > 0
                    ? string.Format(System.Globalization.CultureInfo.InvariantCulture, UiText.ConsoleFps, 1000 / app.FrameMs, app.FrameMs)
                    : UiText.ConsoleNoFps);
                return;
            case "gallery":
                // The forms toolkit's controls, over whatever is showing.
                Toggle();
                Forms.GalleryModel.Show(app);
                return;
            case "help":
                Say(UiText.ConsoleHelp);
                Say(UiText.ConsoleCheatHelp);
                return;
            case "cheats" when words.Length > 1 && words[1].Equals("on", StringComparison.OrdinalIgnoreCase):
                CheatsOn = true;
                Say(UiText.ConsoleCheatsOn);
                return;
            case "cheats" when words.Length > 1 && words[1].Equals("off", StringComparison.OrdinalIgnoreCase):
                CheatsOn = false;
                Say(UiText.ConsoleCheatsOff);
                return;
            case "players" when session is not null:
                foreach (var p in session.Game.Players)
                {
                    Say(string.Format(CultureInfo.InvariantCulture, "{0} {1}{2}{3}", p.Index, p.Name,
                        p == session.Local ? UiText.ConsoleYou : "", p.Eliminated ? UiText.ConsoleOut : ""));
                }

                return;
            case "hash" when session is not null:
                Say(session.Game.Hash().ToString("x16", CultureInfo.InvariantCulture));
                return;
        }

        if (!_cheats.ContainsKey(command) && command != "devwindow")
        {
            Say(UiText.ConsoleUnknown);
            return;
        }

        if (!CheatsOn)
        {
            Say(UiText.ConsoleCheatsAreOff);
            return;
        }

        if (session is null)
        {
            Say(UiText.ConsoleNoGame);
            return;
        }

        if (command == "devwindow")
        {
            Close();
            app.Switch(ScreenMode.Developer, push: true);
            return;
        }

        var target = session.Local;
        if (words.Length > 1)
        {
            if (!int.TryParse(words[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= session.Game.Players.Count)
            {
                Say(UiText.ConsoleNoPlayer);
                return;
            }

            target = session.Game.Players[index];
        }

        app.GameEvents(session.Send(new CheatCommand(target.Index, _cheats[command])));
        Say(string.Format(CultureInfo.InvariantCulture, UiText.ConsoleDone, command, target.Name));
        if (_cheats[command] == Cheat.SpecialTest)
        {
            Say(session.Game.Hash().ToString("x16", CultureInfo.InvariantCulture));
        }
    }

    private void Say(string text)
    {
        _output.Add(text);
        if (_output.Count > 100)
        {
            _output.RemoveAt(0);
        }
    }

    /// <summary>The console over the top of the screen: the last lines and the line being typed.</summary>
    public void Draw(Framebuffer target, UiAssets assets)
    {
        if (!Open)
        {
            return;
        }

        var font = assets.Fonts.ForIndex(1);
        var height = (Lines + 1) * LineHeight + 6;
        target.FillRect(0, 0, target.Width, height, Back);
        target.FillRect(0, height, target.Width, 1, Prompt);
        var shown = _output.Skip(Math.Max(0, _output.Count - Lines)).ToList();
        for (var i = 0; i < shown.Count; i++)
        {
            font.Draw(target, shown[i], 4, 2 + (i + 1) * LineHeight, 0, 0, Ink, 0);
        }

        font.Draw(target, "] " + Input + "_", 4, 2 + (Lines + 1) * LineHeight, 0, 0, Prompt, 0);
    }
}
