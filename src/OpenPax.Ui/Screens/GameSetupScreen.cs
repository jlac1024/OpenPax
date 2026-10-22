using OpenPax.Data.Views;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Screens;

/// <summary>
/// Game Setup (VIEW 10000, palette 5): the galaxy preview and its size,
/// opponents, difficulty, and the empire's name, fleet color and flag.
/// Start makes the game.
/// </summary>
public sealed class GameSetupScreen : Screen
{
    public const int ViewId = 10000;
    public const int PaletteIndex = 5;

    private const int Preview = 10001;
    private const int Stars = 10002;
    private const int Opponents = 10003;
    private const int Difficulty = 10004;
    private const int EmpireName = 10005;
    private const int Fleet = 10006;
    private const int FlagPanel = 10007;
    private const int Start = 10100;
    private const int Cancel = 10101;
    private const int NewUniverse = 10102;
    private const int FleetPrevious = 10103;
    private const int FleetNext = 10104;
    private const int FlagPrevious = 10105;
    private const int FlagNext = 10106;

    public GameSetupScreen(UiApp app)
        : base(app, new ViewWindow(app.Assets, ViewId, PaletteIndex, ViewId, MakePanel))
    {
        app.Assets.Cursor.Show(MouseCursor.Arrow);
        // The galaxy's defaults stand: 20 stars, 15 opponents, difficulty 3.
        // The name, flag and fleet start on the player's Options (the port's
        // own; the original starts on "Your Name", which stands while no
        // name is set).
        Window.Get<TextEditPane>(EmpireName).Text = app.PlayerName.Length > 0 ? app.PlayerName : app.Assets.Strings.Get(new TextRef(ViewId, 8));
        Window.Get<FlagPane>(FlagPanel).Set(app.Preferences.Get(Preferences.FlagId));
        Window.Get<FleetColorPane>(Fleet).Set(app.Preferences.Get(Preferences.FleetId));
        PreviewPane.Generate(DefaultSeed());
        PreviewPane.Count = Window.Get<SpinnerPane>(Stars).Value;
    }

    private UniversePreviewPane PreviewPane => Window.Get<UniversePreviewPane>(Preview);

    /// <summary>
    /// The default seed: the original mixes the system time into a number
    /// below 25000. Any clock value does; it never reaches the game.
    /// </summary>
    private static uint DefaultSeed()
    {
        return (uint)(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond % 25000);
    }

    private static Pane? MakePanel(ViewNode node, int left, int top)
    {
        return node.ClassId switch
        {
            10001 => new UniversePreviewPane(node, left, top),
            10002 when node.Details is ByteOptionDetails d => new FleetColorPane(node, left, top, d),
            10003 when node.Details is ByteOptionDetails d => new FlagPane(node, left, top, d),
            _ => null,
        };
    }

    public override void Handle(UiMessage message, long now)
    {
        switch (message.Message)
        {
            case Stars:
                PreviewPane.Count = Math.Min(message.Value, 100);
                break;
            case NewUniverse:
                PreviewPane.NewUniverse();
                break;
            case FleetPrevious or FleetNext:
                Window.Get<FleetColorPane>(Fleet).Step(message.Message == FleetNext ? 1 : -1);
                break;
            case FlagPrevious or FlagNext:
                Window.Get<FlagPane>(FlagPanel).Step(message.Message == FlagNext ? 1 : -1);
                break;
            case Start when App.ChosenSpecies is { } species:
                App.Request(new HostRequest(HostRequestKind.StartGame, Choices: new NewGameChoices(
                    PreviewPane.Seed,
                    PreviewPane.Count,
                    Window.Get<SpinnerPane>(Opponents).Value,
                    Window.Get<SpinnerPane>(Difficulty).Value,
                    Window.Get<TextEditPane>(EmpireName).Text,
                    Window.Get<FlagPane>(FlagPanel).Flag,
                    Window.Get<FleetColorPane>(Fleet).Color,
                    species)));
                break;
            case Cancel:
                App.Switch(ScreenMode.SpeciesSelection);
                break;
        }
    }
}
