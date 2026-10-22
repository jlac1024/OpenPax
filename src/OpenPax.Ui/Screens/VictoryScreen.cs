using OpenPax.Audio;
using OpenPax.Data.Views;
using OpenPax.Sim.Commands;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Screens;

/// <summary>
/// The end window (mode 0, VIEW 16000 "victory window"): VICTORY or DEFEAT
/// over its own art, and Quit Pax, New Game, or Observe Game, which shows
/// the player every system and goes on to the bridge to watch.
/// </summary>
public sealed class VictoryScreen : Screen
{
    public const int ViewId = 16000;

    private const int Verdict = 16001;
    private const int Quit = 16002;
    private const int NewGame = 16003;
    private const int Observe = 16004;
    private const int OutcomeList = 110;

    public VictoryScreen(UiApp app)
        : base(app, Make(app))
    {
        // The defeated see the darker art: palette 14, background 16001, button pictures 16002 on.
        var won = !Lost(app);
        app.Assets.Cursor.Show(won ? MouseCursor.Victory : MouseCursor.Defeat);
        var firstPicture = won ? 16005 : 16002;
        for (var i = 0; i < 3; i++)
        {
            Window.Get<ButtonPane>(Quit + i).Picture = firstPicture + i;
        }

        Window.Get<TextLinePane>(Verdict).Text = app.Assets.Strings.Get(new TextRef(OutcomeList, won ? 1 : 0));
        Window.Get(Observe).Enabled = true;
        app.Sound.Play(SoundIds.SimulationComplete);
    }

    private static bool Lost(UiApp app) => app.Session?.Local.Eliminated ?? true;

    private static ViewWindow Make(UiApp app) => Lost(app)
        ? new ViewWindow(app.Assets, ViewId, 14, 16001)
        : new ViewWindow(app.Assets, ViewId, 13, 16000);

    public override void Handle(UiMessage message, long now)
    {
        switch (message.Message)
        {
            case Quit:
                App.QuitGame();
                break;
            case NewGame:
                App.NewGame();
                break;
            case Observe when App.Session is { } session:
                session.Send(new ObserveCommand(session.Local.Index));
                App.Switch(ScreenMode.MainControl);
                break;
            default:
                break;
        }
    }
}
