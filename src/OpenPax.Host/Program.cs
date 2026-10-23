using OpenPax.Audio;
using OpenPax.Data.Import;
using OpenPax.Data.Png;
using OpenPax.Host;
using OpenPax.Host.Headless;
using OpenPax.Host.Platform;
using OpenPax.Render;
using OpenPax.Sdl;
using OpenPax.Ui;

var options = HostOptions.Parse(args, Console.Error);
if (options is null)
{
    return 2;
}

if (options is { ImportFrom: { } from, ImportTo: { } to })
{
    // From GOG's offline installer, as the first run does, or from a game folder.
    return File.Exists(from) && from.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        ? InstallerImport.Run(from, to, options.Ffmpeg, Console.Out) ? 0 : 1
        : Importer.Run(from, to, options.Ffmpeg, Console.Out) ? 0 : 1;
}

if (options.UpscaleData is { } upscaleData)
{
    Console.WriteLine(string.Format(Strings.Upscaled, OpenPax.Data.Import.UpscaleImporter.UpscaleAll(upscaleData)));
    return 0;
}

// Before anything that may reach SDL (the user folder is SDL's preference path).
NativeLibraries.Register();

// A crash says what went wrong and leaves a log, even with no console (Windows).
CrashReport.Install(UserFolder.Resolve());

// What a past update left behind (docs/updates.md): the old files, now that the old game has closed.
var installFolder = OpenPax.Host.Update.InstallFolder.Of(AppContext.BaseDirectory);
OpenPax.Host.Update.UpdateInstaller.CleanUp(installFolder);
var dataFolder = DataFolder(UserFolder.Resolve());

// With no game data the window's first run imports it (docs/first-run.md); the modes with no window cannot.
var windowless = options.Dedicated is not null || options.VerifyReplay is not null || options.SoakTicks is not null || options.HeadlessTicks is not null;
if (windowless && !Importer.IsImported(dataFolder))
{
    Console.Error.WriteLine(string.Format(Strings.NoData, dataFolder));
    return 1;
}

if (options.Dedicated is not null)
{
    // A game server with no window: Ctrl+C stops it.
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        stop.Cancel();
    };
    // Its mods, from the user folder's mods folder and the game's own, as --mods names them.
    var serverMods = options.Mods.Count == 0 ? OpenPax.Modding.ModHost.None : OpenPax.Modding.ModHost.Load(ModsFolders(UserFolder.Resolve()), options.Mods);
    foreach (var error in serverMods.Errors)
    {
        Console.Error.WriteLine(error);
    }

    var serverFiles = serverMods.Data(dataFolder);
    return OpenPax.Host.Headless.DedicatedServer.Run(
        options,
        new GameLauncher(serverFiles, serverMods).Tables,
        Console.Out,
        stop.Token,
        new StringTable(OpenPax.Data.Resources.ResourceFile.Load(serverFiles.Path("original", "Data", "StrData"))),
        serverMods);
}

if (options.VerifyReplay is { } replay)
{
    return HeadlessRunner.Verify(dataFolder, replay, Console.Out);
}

if (options.SoakTicks is { } soakTicks)
{
    var settings = new Replay(options.Seed, options.Stars, options.Players, options.Difficulty, []);
    return SoakRunner.Run(dataFolder, settings, soakTicks, Console.Out);
}

if (options.HeadlessTicks is { } ticks)
{
    var settings = new Replay(options.Seed, options.Stars, options.Players, options.Difficulty, []);
    return HeadlessRunner.Record(dataFolder, settings, ticks, options.ReplayOut, Console.Out);
}

if (!Sdl.Init(InitFlags.Video | InitFlags.Events))
{
    Console.Error.WriteLine(string.Format(Strings.SdlInitFailed, Sdl.GetError()));
    return 1;
}

try
{
    // A window, when not fullscreen, is the largest whole multiple of the
    // original's 640x480 that leaves room on the display (at least 1x).
    var desktop = Sdl.DesktopSize() ?? (ScreenWidth * 2, ScreenHeight * 2);
    var multiple = Math.Max(1, Math.Min(desktop.Width * 9 / 10 / ScreenWidth, desktop.Height * 9 / 10 / ScreenHeight));
    // Where the player left it, on the same display (WindowPlacement); scripted
    // runs always open the same way, so their pictures do not depend on the displays.
    var earlyUserFolder = UserFolder.Resolve();
    var earlySettings = ConfigFile.EarlyPreferences(earlyUserFolder is null ? null : Path.Combine(earlyUserFolder, ConfigFile.FileName));
    var placed = options.Screenshot is null && options.Script.Count == 0 && options.ExitAfterMilliseconds is null;
    var window = WindowPlacement.Open(Strings.WindowTitle, (ScreenWidth * multiple, ScreenHeight * multiple), earlySettings, placed);
    if (window == 0)
    {
        Console.Error.WriteLine(string.Format(Strings.WindowFailed, Sdl.GetError()));
        return 1;
    }

    WindowIcon.Apply(window);

    using var screen = Screen.Create(window, ScreenWidth, ScreenHeight);
    if (screen is null)
    {
        Console.Error.WriteLine(string.Format(Strings.RendererFailed, Sdl.GetError()));
        Sdl.DestroyWindow(window);
        return 1;
    }

    // The first run: the player's copy of the game is imported before anything else.
    if (!Importer.IsImported(dataFolder) && !OpenPax.Host.Setup.FirstRun.Run(window, screen, dataFolder, options.Screenshot, options.ExitAfterMilliseconds))
    {
        return 0;
    }


    // Mods load first (docs/modding.md): their data packs lie over the game's data for everything read after.
    var userFolder = UserFolder.Resolve();
    var configPath = userFolder is null ? null : Path.Combine(userFolder, ConfigFile.FileName);
    var modsFolders = ModsFolders(userFolder);
    var enabledMods = configPath is null ? [] : ConfigFile.EnabledMods(configPath);
    var runtime = ModRuntime.Own(dataFolder, modsFolders, enabledMods);
    var frame = new Framebuffer(ScreenWidth, ScreenHeight);
    var app = new UiApp(runtime.Assets);
    if (configPath is not null)
    {
        ConfigFile.Load(configPath, dataFolder, app, Console.Error);
    }

    // Updates from GitHub (docs/updates.md): asked on start when the player
    // allows it, never for a development build, which has no release to follow.
    using var updater = new OpenPax.Host.Update.Updater(installFolder)
    {
        Running = OpenPax.Host.Update.AppVersion.ForTesting() ?? OpenPax.Host.Update.AppVersion.Current,
    };
    if (app.Preferences.Flag(Preferences.CheckUpdates) && !updater.Running.IsDevelopment && options.Script.Count == 0 && options.Screenshot is null)
    {
        updater.Check(app.Preferences.Flag(Preferences.UpdatePreReleases), say: false);
    }

    app.EnabledMods.AddRange(enabledMods);
    app.OwnModsLoaded();
    var ignored = new List<string>();
    app.AvailableMods.AddRange(OpenPax.Modding.ModCatalog.Find(modsFolders, ignored)
        .Select(m => new ModListing(m.Manifest.Id, m.Manifest.Name, m.Manifest.Version, m.Manifest.Description)));

    var (lobbyAddress, lanHosts) = GameConfig.Read(Path.Combine(AppContext.BaseDirectory, GameConfig.FileName), Console.Error);
    if (lobbyAddress is not null)
    {
        app.PublicLobbyAddress = lobbyAddress;
    }

    app.LanHosts.AddRange(lanHosts);

    // Sound plays when a device opens; without one the game runs silent.
    using var audio = AudioOutput.Open();

    // Fullscreen covers the display at its own resolution (the Fullscreen setting; Alt+Enter switches).
    // Scripted runs stay a window, so their pictures do not depend on the display.
    var scripted = options.Screenshot is not null || options.Script.Count > 0 || options.ExitAfterMilliseconds is not null;
    var fullscreen = !scripted && app.Preferences.Flag(Preferences.Fullscreen);
    Sdl.SetWindowFullscreen(window, fullscreen);

    // What the app takes from a mod set; again when another set is loaded in place.
    void Use(ModRuntime next)
    {
        runtime = next;
        app.Sound = next.Mixer;
        app.Tables = () => next.Launcher.Tables;
        app.ModFingerprint = next.Mods.Fingerprint;
        foreach (var problem in next.Problems)
        {
            Console.Error.WriteLine(problem);
            app.Post(problem);
        }

        if (userFolder is not null)
        {
            app.SaveGames = new SaveFolder(Path.Combine(userFolder, SaveFolder.FolderName), next.Launcher, Console.Error);
            var strings = next.Assets.Strings;
            app.CustomSpeciesStore = new SpeciesFile(
                Path.Combine(userFolder, SpeciesFile.FileName),
                strings.Get(new OpenPax.Data.Views.TextRef(OpenPax.Data.Tables.RaceTemplate.DefaultNameString.List, OpenPax.Data.Tables.RaceTemplate.DefaultNameString.Index)),
                strings.Get(new OpenPax.Data.Views.TextRef(OpenPax.Data.Tables.RaceTemplate.DefaultDescriptionString.List, OpenPax.Data.Tables.RaceTemplate.DefaultDescriptionString.Index)),
                Console.Error);
        }
    }

    Use(runtime);

    // As a room's host: a package of a loaded mod, for the lobby to pass on.
    app.PackMod = key => runtime.Mods.Mods.FirstOrDefault(m => m.Key.ToString() == key) is { } mod
        ? OpenPax.Modding.ModPackage.Pack(mod.Folder)
        : null;

    // A package from a room's host, kept apart from the player's own mods, one folder by id and hash.
    app.InstallMod = (key, package) =>
    {
        if (OpenPax.Modding.ModKey.Parse(key) is not { } parsed || SyncedFolder(userFolder) is not [var synced]
            || !parsed.Id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') || !parsed.Hash.All(char.IsAsciiHexDigit))
        {
            return false;
        }

        var folder = Path.Combine(synced, parsed.Id + "-" + parsed.Hash);
        return Directory.Exists(folder) || OpenPax.Modding.ModPackage.Unpack(package, folder, parsed.Hash);
    };

    // Another mod set in place (docs/modding/multiplayer.md): the player's own (null) or a
    // room's keys, looked for in the mods folders and the copies fetched from hosts.
    app.LoadMods = set =>
    {
        IReadOnlyList<OpenPax.Modding.ModKey> missing = [];
        var next = set is null
            ? ModRuntime.Own(dataFolder, modsFolders, app.EnabledMods)
            : ModRuntime.ForKeys(dataFolder, [.. modsFolders, .. SyncedFolder(userFolder)], OpenPax.Modding.ModKey.ParseSet(set), out missing);
        if (missing.Count > 0)
        {
            next.Mods.Unload();
            return [.. missing.Select(k => k.ToString())];
        }

        var old = runtime;
        Use(next);
        app.LoadedModSet = set;
        if (set is null)
        {
            app.OwnModsLoaded();
        }

        app.UseAssets(next.Assets);
        old.Mods.Unload();
        return [];
    };

    Sdl.StartTextInput(window);

    // The game draws its own animated pointer, as the original does.
    Sdl.HideCursor();

    // The true-color picture the window shows; the forms and the pointer draw on it.
    var canvas = new OpenPax.Render.TrueColor.Canvas(ScreenWidth, ScreenHeight);
    app.CursorOnCanvas = true;

    var running = true;
    const int ConfigCheckFrames = 60;
    var framesSinceConfig = 0;
    var savedConfig = ConfigFile.Text(app);
    bool Carry(IEnumerable<HostRequest> requests)
    {
        foreach (var request in requests)
        {
            switch (request.Kind)
            {
                case HostRequestKind.Quit:
                    return false;
                case HostRequestKind.PlayMovie when request.Movie is { } movie && !options.NoMovies && app.Preferences.Flag(Preferences.PlayMovies):
                    // the music waits while a movie plays.
                    runtime.Mixer.PauseMusic(true);
                    var played = MoviePlayer.Play(screen, runtime.Files.Path("movies", movie), runtime.Mixer, audio);
                    runtime.Mixer.PauseMusic(false);
                    if (!played)
                    {
                        return false;
                    }

                    break;
                case HostRequestKind.StartGame when request.Choices is { } choices:
                    app.GameStarted(runtime.Launcher.Start(choices), (long)Sdl.GetTicks());
                    break;
                case HostRequestKind.StartNetworkGame when request.Network is { } network:
                    app.HostedGameReady(runtime.Launcher.StartNetwork(network), (long)Sdl.GetTicks());
                    break;
                case HostRequestKind.CheckForUpdates:
                    updater.Check(app.Preferences.Flag(Preferences.UpdatePreReleases), say: true);
                    break;
                case HostRequestKind.Update:
                    updater.Install();
                    break;
                case HostRequestKind.CancelUpdate:
                    updater.Cancel();
                    break;
            }
        }

        return true;
    }

    // Scripted clicks for checks without a person: each is a press and
    // release, then the screen handles it before the next.
    foreach (var step in options.Script)
    {
        if (step.Command is { } command)
        {
            app.Command(command);
        }
        else
        {
            app.MouseDown(step.X, step.Y, 1, 0);
            app.MouseUp(step.X, step.Y);
        }

        running = running && Carry(app.Tick(0));
    }

    // The original stops everything, the game clock included, while the
    // window is not the active one.
    var focused = true;

    // Frames at the display's refresh rate (60 when it cannot be told); vsync by the VSync setting.
    var pacer = new FramePacer(Sdl.GetTicksNs, Sdl.DelayPrecise);
    pacer.SetRate(Sdl.RefreshRate() ?? 60);
    var vsyncWanted = app.Preferences.Flag(Preferences.VSync);
    var vsync = vsyncWanted && screen.SetVSync(true);
    var heldButton = 0;
    var formsHeld = false;
    while (running)
    {
        var now = (long)Sdl.GetTicks();
        while (Sdl.PollEvent(out var ev))
        {
            switch (ev.Type)
            {
                case EventType.Quit or EventType.WindowCloseRequested:
                    running = false;
                    break;
                case EventType.MouseButtonDown when ev.MouseButton is 1 or 3 && heldButton == 0:
                    {
                        // The right button acts as a left press flagged as right.
                        // A form over the window takes the press first.
                        heldButton = ev.MouseButton;
                        var (cx, cy) = screen.ToCanvas(ev.MouseX, ev.MouseY);
                        formsHeld = app.FormsMouseDown(cx, cy, ev.MouseButton == 3);
                        if (!formsHeld)
                        {
                            var (x, y) = screen.ToScreen(ev.MouseX, ev.MouseY);
                            app.MouseDown(x, y, ev.MouseClicks, now, ev.MouseButton == 3);
                        }

                        break;
                    }

                case EventType.MouseMotion:
                    {
                        var (cx, cy) = screen.ToCanvas(ev.MouseX, ev.MouseY);
                        var overForm = app.FormsMouseMove(cx, cy);
                        var (x, y) = screen.ToScreen(ev.MouseX, ev.MouseY);
                        if (!overForm || !formsHeld)
                        {
                            app.MouseMove(x, y, now);
                        }

                        break;
                    }

                case EventType.MouseButtonUp when ev.MouseButton == heldButton:
                    {
                        heldButton = 0;
                        var (cx, cy) = screen.ToCanvas(ev.MouseX, ev.MouseY);
                        if (formsHeld)
                        {
                            app.FormsMouseUp(cx, cy);
                        }
                        else
                        {
                            var (x, y) = screen.ToScreen(ev.MouseX, ev.MouseY);
                            app.MouseUp(x, y);
                        }

                        formsHeld = false;
                        break;
                    }

                case EventType.MouseWheel:
                    app.FormsWheel(ev.WheelY > 0 ? 1 : ev.WheelY < 0 ? -1 : 0);
                    break;
                case EventType.WindowMouseLeave:
                    app.Pointer = null;
                    app.CanvasPointer = null;
                    break;
                case EventType.WindowFocusGained or EventType.WindowFocusLost:
                    focused = ev.Type == EventType.WindowFocusGained;
                    break;
                case EventType.WindowMoved or EventType.WindowResized or EventType.WindowMaximized or EventType.WindowRestored
                    or EventType.WindowEnterFullscreen or EventType.WindowLeaveFullscreen when !scripted:
                    WindowPlacement.Follow(window, ev.Type, app.Preferences);
                    break;
                case EventType.KeyDown when ev.Key == Sdl.KeyReturn && (ev.KeyMod & Sdl.ModAlt) != 0:
                    {
                        app.Preferences.Set(Preferences.Fullscreen, app.Preferences.Flag(Preferences.Fullscreen) ? 0 : 1);
                        break;
                    }

                case EventType.KeyDown:
                    if (KeyMap.ToUiKey(ev.Key) is { } key)
                    {
                        app.Key(key, (ev.KeyMod & Sdl.ModShift) != 0, now);
                    }

                    if (!app.FormsHaveFocus && KeyMap.ToCommand(ev.Key, ev.KeyMod) is { } command)
                    {
                        app.Command(command);
                    }

                    break;
                case EventType.TextInput:
                    app.Typed(Sdl.TextOf(ev), now);
                    break;
            }
        }

        if (focused)
        {
            running = running && Carry(app.Tick(now));
        }

        // The new version is in place: start it, and this one closes.
        if (updater.Follow(app.Updates))
        {
            if (updater.Restart())
            {
                running = false;
            }
            else
            {
                app.Tell(OpenPax.Host.Strings.UpdateRestartYourself);
            }
        }

        // Alt+Enter and Options both just change the setting; the window follows it here.
        if (!scripted && app.Preferences.Flag(Preferences.Fullscreen) != fullscreen)
        {
            fullscreen = !fullscreen;
            Sdl.SetWindowFullscreen(window, fullscreen);
        }

        // The frame at a whole-number scale of the window, as large as the screen grows (docs/ui/widescreen.md).
        var (windowWidth, windowHeight) = screen.OutputSize;
        app.FitWindow(windowWidth, windowHeight);
        var scale = app.FrameScale;
        runtime.Assets.Upscaled = app.Preferences.Flag(Preferences.UpscaledArt) && scale % 2 == 0;
        (screen.ClassicWidth, screen.ClassicHeight) = (app.ClassicWidth, app.ClassicHeight);
        if (frame.Scale != scale || frame.Width != app.ClassicWidth || frame.Height != app.ClassicHeight)
        {
            frame = new Framebuffer(app.ClassicWidth, app.ClassicHeight, scale);
        }

        runtime.Assets.Fonts.Settings.Mode = (OpenPax.Render.TextMode)Math.Clamp(app.Preferences.Get(Preferences.TextFont), 0, 2);
        runtime.Assets.Fonts.Settings.Outline = (OpenPax.Render.TextOutline)Math.Clamp(app.Preferences.Get(Preferences.TextOutline), 0, 2);
        frame.Palette = runtime.Assets.Palette(app.PaletteIndex);
        frame.Clear(0);
        app.Draw(frame);
        audio?.Pump(runtime.Mixer);
        var palette = runtime.Assets.Palette(app.PaletteIndex);
        var (outputWidth, outputHeight) = screen.OutputSize;
        canvas.Resize(outputWidth, outputHeight);
        var classic = screen.Classic;
        app.DrawCanvas(canvas, classic);
        screen.Present(frame, palette, canvas);

        if (options.Screenshot is { } shot)
        {
            // The whole window as shown: the original screens scaled, the forms and pointer over them.
            var whole = new OpenPax.Render.TrueColor.Canvas(canvas.Width, canvas.Height);
            whole.Clear(OpenPax.Render.TrueColor.Color.Black);
            whole.DrawClassic(frame, palette, classic.X, classic.Y, classic.Width, classic.Height);
            whole.Overlay(canvas);
            using (var file = File.Create(shot))
            {
                PngWriter.WriteRgba(file, whole.Width, whole.Height, System.Runtime.InteropServices.MemoryMarshal.AsBytes(whole.Pixels.AsSpan()));
            }

            running = false;
        }

        if (options.ExitAfterMilliseconds is { } limit && Sdl.GetTicks() >= (ulong)limit)
        {
            running = false;
        }

        // Settings changed (a name typed, an option set) are written within about a second.
        if (configPath is not null && !scripted && ++framesSinceConfig >= ConfigCheckFrames)
        {
            framesSinceConfig = 0;
            if (ConfigFile.Text(app) is var config && config != savedConfig)
            {
                ConfigFile.Save(configPath, app);
                savedConfig = config;
            }
        }

        // The setting may change in Options; with vsync working, presenting has already waited.
        if (app.Preferences.Flag(Preferences.VSync) != vsyncWanted)
        {
            vsyncWanted = !vsyncWanted;
            if (vsyncWanted)
            {
                vsync = screen.SetVSync(true);
            }
            else
            {
                screen.SetVSync(false);
                vsync = false;
            }
        }

        pacer.EndFrame(vsync);
        app.FrameMs = pacer.LastFrameMs;
    }

    // A scripted run (screenshots, clicks) leaves the player's config alone.
    if (configPath is not null && !scripted && ConfigFile.Text(app) != savedConfig)
    {
        ConfigFile.Save(configPath, app);
    }

    screen.Dispose();
    Sdl.DestroyWindow(window);
    return 0;
}
finally
{
    Sdl.Quit();
}

internal static partial class Program
{
    /// <summary>The mods folder in the user folder (docs/modding.md).</summary>
    private const string ModsFolderName = "mods";

    /// <summary>Where mods fetched from hosts are kept (docs/modding/multiplayer.md), one folder each by id and hash, apart from the player's own.</summary>
    private const string SyncedFolderName = "mods-synced";

    private static string[] SyncedFolder(string? userFolder) => userFolder is null ? [] : [Path.Combine(userFolder, SyncedFolderName)];

    /// <summary>Where mods are found: the user folder's mods folder, then the ones shipped next to the game (deploy/mods), the user's copy winning.</summary>
    private static string[] ModsFolders(string? userFolder) =>
        userFolder is null
            ? [Path.Combine(AppContext.BaseDirectory, ModsFolderName)]
            : [Path.Combine(userFolder, ModsFolderName), Path.Combine(AppContext.BaseDirectory, ModsFolderName)];

    internal const int ScreenWidth = 640;
    internal const int ScreenHeight = 480;

    /// <summary>
    /// Where the imported game data is (docs/first-run.md): beside the
    /// program when an import is there (a build from source, deploy/), else
    /// the user folder's data folder, which the first run fills.
    /// </summary>
    private static string DataFolder(string? userFolder)
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "data");
        return Importer.IsImported(beside) || userFolder is null ? beside : Path.Combine(userFolder, "data");
    }
}
