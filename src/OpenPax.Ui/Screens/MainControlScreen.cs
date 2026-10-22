using OpenPax.Data.Views;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Screens;

/// <summary>
/// The Main control screen, the bridge (VIEW 22000, palette 0), which the
/// game opens on. Seven stations open the game's windows; hovering one
/// lights it and names it. A small galaxy map opens the Sector Display on a
/// double click. The date and the newest notice show at the top.
/// </summary>
public sealed class MainControlScreen : Screen
{
    public const int ViewId = 22000;
    public const int PaletteIndex = 0;

    private const int FirstStation = 22001;
    private const int Stations = 7;
    private const int Map = 22008;
    private const int MessageLine = 22009;
    private const int Date = 22109;
    private const int LabelOffset = 100;
    private const int AnimationOffset = 200;
    private const int HighlightFrames = 50;
    private const int LabelDelay = 100;

    private int _hovered;
    private long _hoverSince;

    public MainControlScreen(UiApp app)
        : base(app, new ViewWindow(app.Assets, ViewId, PaletteIndex, ViewId, MakePanel))
    {
        app.Assets.Cursor.Show(MouseCursor.Bridge);
        for (var i = 0; i < Stations; i++)
        {
            Window.Get<AnimationPane>(FirstStation + AnimationOffset + i).Play(0, true);
        }

        var map = Window.Get<MapPane>(Map);
        map.Session = app.Session;
        map.AltColors = true;

        // the control room's hum, until the bridge closes.
        app.Sound.Play(Audio.SoundIds.BridgeAmbience, loop: true);
    }

    public override void Closed()
    {
        App.Sound.Stop(Audio.SoundIds.BridgeAmbience);
    }

    private static Pane? MakePanel(ViewNode node, int left, int top)
    {
        return node.ClassId switch
        {
            9000 when node.Details is ByteOptionDetails d => new MapPane(node, left, top, d),
            22002 => new MessageLinePane(node, left, top),
            _ => null,
        };
    }

    public override void Handle(UiMessage message, long now)
    {
        switch (message.Message)
        {
            case UiMessage.Within when message.Value is >= FirstStation and < FirstStation + Stations:
                Hover(message.Value, now);
                Window.Get<AnimationPane>(message.Value + AnimationOffset).PictureOffset = HighlightFrames;
                break;
            case UiMessage.Left:
                // every station goes back to its plain frames.
                for (var i = 0; i < Stations; i++)
                {
                    Window.Get<AnimationPane>(FirstStation + AnimationOffset + i).PictureOffset = 0;
                }

                Hover(0, now);
                break;
            case Map:
                // A double click on the map's star opens the Sector Display.
                App.Switch(ScreenMode.SectorDisplay, push: true);
                break;
            case MessageLine:
                App.Log.Current?.Action?.Invoke(App);
                break;
            case FirstStation:
                // The Fleet Command station.
                App.Switch(ScreenMode.FleetCommand, push: true);
                break;
            case FirstStation + 1:
                App.Switch(ScreenMode.Research, push: true);
                break;
            case FirstStation + 2:
                // The Foreign Affairs station.
                App.Switch(ScreenMode.ForeignAffairs, push: true);
                break;
            case FirstStation + 3:
                // The Domestic Policy station.
                App.Switch(ScreenMode.DomesticPolicy, push: true);
                break;
            case FirstStation + 4:
                // The Ship Design station.
                App.Switch(ScreenMode.ShipDesign, push: true);
                break;
            case FirstStation + 6:
                // The Game Options station.
                App.Switch(ScreenMode.GameOptions, push: true);
                break;
            case FirstStation + 5:
                // The Planetary Surface station.
                App.Switch(ScreenMode.PlanetarySurface, push: true);
                break;
            default:
                // The other stations' windows (modes 14 to 19) are not ported yet.
                break;
        }
    }

    /// <summary>A station's name shows after the pointer has rested on it for 100 ms.</summary>
    private void Hover(int station, long now)
    {
        if (station != _hovered)
        {
            if (_hovered != 0)
            {
                Window.Get(_hovered + LabelOffset).Visible = false;
            }

            _hovered = station;
            _hoverSince = now;
        }
        else if (station != 0 && now - _hoverSince >= LabelDelay)
        {
            Window.Get(station + LabelOffset).Visible = true;
        }
    }

    public override void Tick(long now)
    {
        if (App.Session is { } session)
        {
            Window.Get<TextLinePane>(Date).Text = session.Date;
        }

        var line = Window.Get<MessageLinePane>(MessageLine);
        line.Text = App.Notice;
        line.Priority = App.Log.Current?.Priority ?? 0;
    }
}
