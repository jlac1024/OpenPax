using OpenPax.Data.Art;
using OpenPax.Data.Import;
using OpenPax.Data.Install;
using OpenPax.Render;
using OpenPax.Render.Fonts;
using OpenPax.Sdl;
using OpenPax.Ui.Setup;

namespace OpenPax.Host.Setup;

/// <summary>
/// The first run (docs/first-run.md): with no imported game data, the setup
/// screen asks for the player's own copy and imports it into the data folder
/// before anything else of the game starts. It needs nothing of the game:
/// its text is the modern font shipped beside the program.
/// </summary>
internal static class FirstRun
{
    // What ships beside the program for this: the modern font, and the movie converter.
    private const string FontsFolder = "fonts";
    private const string ToolsFolder = "tools";

    // A scripted run's picture of the setup is taken once it has had time to lay out.
    private const long ScreenshotAt = 1000;

    /// <summary>Runs the setup in the window; true once the game is imported, false when the player quits.</summary>
    /// <param name="screenshot">For scripted runs: the setup as drawn is saved here after a second, and it quits.</param>
    /// <param name="exitAfter">For scripted runs: it quits after this many milliseconds.</param>
    public static bool Run(nint window, Screen screen, string dataFolder, string? screenshot = null, long? exitAfter = null)
    {
        var fonts = Path.Combine(AppContext.BaseDirectory, FontsFolder);
        var installs = InstallFinder.Find();
        var model = new SetupModel(installs, (source, progress, log) => Import(source, dataFolder, fonts, progress, log), installs.Count == 0 && InstallFinder.GalaxyStubDownloaded());
        var app = new SetupApp(ModernFont.Load(fonts), model);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        model.FolderWanted += () => Sdl.Sdl.ShowOpenFolderDialog(window, home);
        model.InstallerWanted += () => Sdl.Sdl.ShowOpenFileDialog(window, Strings.InstallerFilter, "exe", home);

        // The classic picture stays black behind the setup's canvas.
        var frame = new Framebuffer(Program.ScreenWidth, Program.ScreenHeight);
        var black = new Palette(new Rgb[Palette.Size]);
        var canvas = new OpenPax.Render.TrueColor.Canvas(Program.ScreenWidth, Program.ScreenHeight);
        Sdl.Sdl.StartTextInput(window);
        while (!model.Done)
        {
            var now = (long)Sdl.Sdl.GetTicks();
            while (Sdl.Sdl.PollEvent(out var ev))
            {
                switch (ev.Type)
                {
                    case EventType.Quit or EventType.WindowCloseRequested when !model.Busy:
                        return false;
                    case EventType.MouseButtonDown when ev.MouseButton == 1:
                        {
                            var (x, y) = screen.ToCanvas(ev.MouseX, ev.MouseY);
                            app.MouseDown(x, y);
                            break;
                        }

                    case EventType.MouseButtonUp when ev.MouseButton == 1:
                        {
                            var (x, y) = screen.ToCanvas(ev.MouseX, ev.MouseY);
                            app.MouseUp(x, y);
                            break;
                        }

                    case EventType.MouseMotion:
                        {
                            var (x, y) = screen.ToCanvas(ev.MouseX, ev.MouseY);
                            app.MouseMove(x, y);
                            break;
                        }

                    case EventType.MouseWheel:
                        app.Wheel(ev.WheelY > 0 ? 1 : -1);
                        break;
                    case EventType.KeyDown when KeyMap.ToUiKey(ev.Key) is { } key:
                        app.Key(key, (ev.KeyMod & Sdl.Sdl.ModShift) != 0);
                        break;
                    case EventType.TextInput:
                        app.Text(Sdl.Sdl.TextOf(ev));
                        break;
                }
            }

            while (Sdl.Sdl.TakeDialogResult(out _, out var path, out _))
            {
                model.Picked(path);
            }

            if (model.Quit)
            {
                return false;
            }

            app.Tick(now);
            var (width, height) = screen.OutputSize;
            canvas.Resize(width, height);
            app.Draw(canvas);
            screen.Present(frame, black, canvas);
            if (screenshot is not null && now >= ScreenshotAt)
            {
                using var file = File.Create(screenshot);
                OpenPax.Data.Png.PngWriter.WriteRgba(file, canvas.Width, canvas.Height, System.Runtime.InteropServices.MemoryMarshal.AsBytes(canvas.Pixels.AsSpan()));
                return false;
            }

            if (exitAfter is { } limit && now >= limit)
            {
                return false;
            }

            Sdl.Sdl.Delay(16);
        }

        return true;
    }

    /// <summary>The import, on the setup's own thread: from a game folder, or from GOG's installer unpacked first.</summary>
    private static bool Import(string source, string dataFolder, string fonts, IProgress<ImportProgress> progress, TextWriter log)
    {
        var ffmpeg = Path.Combine(AppContext.BaseDirectory, ToolsFolder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        var modern = Directory.Exists(fonts) ? fonts : null;
        var tool = File.Exists(ffmpeg) ? ffmpeg : null;
        if (File.Exists(source) && source.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return InstallerImport.Run(source, dataFolder, tool, log, progress, modern);
        }

        return Importer.Run(source, dataFolder, tool, log, progress, modern);
    }
}
