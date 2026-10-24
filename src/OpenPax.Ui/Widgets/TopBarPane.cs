using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// The bar across the top of every game window from the Sector Display on
/// (the message log's pane, 640x20 at 0, 0): the current notice, a lamp in
/// its urgency's color that opens the history (the 9 newest notices),
/// the date, and the back button. A double click on a notice runs its
/// action. It is added last, so it draws on top and is hit first.
/// </summary>
public sealed class TopBarPane(UiApp app) : Pane(_layout, 0, 0)
{
    private const int Lamp = 0;
    private const int Back = 1;
    private const int MessageRegion = 2;
    private const int HistoryRegion = 3;
    private const int None = 0xFF;

    private const int BarHeight = 20;
    private const int OpenHeight = 151;
    private const int TextLeft = 36;
    private const int FirstBaseline = 14;
    private const int RowStep = 16;
    private const int Rows = 9;

    private static readonly ViewNode _layout = new(0, new WidgetBase(-1, 0, 0, 640, BarHeight, 1, 1), null, [], null, null);

    // A wide window (docs/ui/widescreen.md): the bar's art widens in the
    // message line, and what lies right of it (lamp, Back, date) moves over.
    private static readonly WideLayout _wide = new(100, 300);
    private const int MessageRight = 437;

    private int _pressed = None;
    private bool _over;
    private bool _double;
    private int _hoveredRow = -1;

    /// <summary>The history list is down (state 1).</summary>
    public bool HistoryOpen { get; private set; }

    public override int Height => HistoryOpen ? OpenHeight : BarHeight;

    public override int Width => 640 + Extra;

    /// <summary>How much wider than 640 the screen under the bar is drawn.</summary>
    private int Extra => Math.Max(0, app.ClassicWidth - 640);

    /// <summary>A point in the bar as the original's 640 wide bar has it: right of the message line, moved back.</summary>
    private int Narrow(int x) => x >= MessageRight + Extra ? x - Extra : Math.Min(x, x >= MessageRight ? MessageRight - 1 : x);

    public void CloseHistory()
    {
        HistoryOpen = false;
    }

    // The history's drop in a wide window: as wide as the message line, so it reaches the lamp
    // (docs/ui/widescreen.md); its art widens in the middle, as the bar's does.
    private static readonly WideLayout _wideDrop = new(89, 300);

    private bool InDrop(int x, int y) => x >= 11 && x < MessageRight + Extra && y >= 0 && y < OpenHeight;

    public override bool HitTest(int x, int y)
    {
        return x >= 0 && x < 640 + Extra && (y >= 0 && y < BarHeight || HistoryOpen && InDrop(x, y));
    }

    /// <summary>Which region a point is in, checked in this order.</summary>
    private int RegionAt(int x, int y)
    {
        if (HistoryOpen && InDrop(x, y))
        {
            return HistoryRegion;
        }

        x = Narrow(x);
        if (x >= 497 && x < 529 && y >= 3 && y < 15)
        {
            return Lamp;
        }

        if (x >= 601 && x < 640 && y >= 0 && y < 19)
        {
            return Back;
        }

        if (!HistoryOpen && x >= 11 && x < 437 && y >= 0 && y < BarHeight)
        {
            return MessageRegion;
        }

        return None;
    }

    /// <summary>The row under a point: the first whose baseline is below it.</summary>
    private int RowAt(int y)
    {
        var bottom = HistoryOpen ? OpenHeight : BarHeight;
        var row = y < FirstBaseline ? 0 : ((y - FirstBaseline) / RowStep) + 1;
        return FirstBaseline + (RowStep * row) <= bottom ? row : -1;
    }

    protected internal override void MouseWithin(int x, int y)
    {
        _hoveredRow = RegionAt(x, y) == HistoryRegion ? RowAt(y) : -1;
    }

    protected internal override void MouseLeft()
    {
        _hoveredRow = -1;
    }

    protected internal override void MouseDown(int x, int y, int clicks)
    {
        _pressed = RegionAt(x, y);
        _over = _pressed != None;
        if (_pressed is Lamp or Back)
        {
            PlaySound(Audio.SoundIds.TopBarPress);
        }

        _double = clicks >= 2;
    }

    protected internal override void MouseMove(int x, int y, long now)
    {
        if (_pressed != None)
        {
            _over = RegionAt(x, y) == _pressed;
        }
    }

    protected internal override void MouseUp(int x, int y)
    {
        var region = _pressed;
        var over = _over;
        (_pressed, _over) = (None, false);

        // A double click on a notice runs its action and closes the list.
        if (_double && app.Log.Current is not null && region == (HistoryOpen ? HistoryRegion : MessageRegion)
            && RowAt(y) is >= 0 and < MessageLog.HistorySize and var row && app.Log.History[row] is { } notice)
        {
            HistoryOpen = false;
            notice.Action?.Invoke(app);
            return;
        }

        if (!over)
        {
            return;
        }

        if (region == Lamp)
        {
            PlaySound(Audio.SoundIds.HistoryToggle);
            HistoryOpen = !HistoryOpen;
        }
        else if (region == Back)
        {
            PlaySound(Audio.SoundIds.BackArrow);
            HistoryOpen = false;

            // The right button goes straight to the bridge, forgetting the way back.
            if (Window?.RightButton ?? false)
            {
                app.ToBridge();
            }
            else
            {
                app.Back();
            }
        }
    }

    protected override void Draw(Framebuffer target)
    {
        var main = Assets.Pictures(ImageSet.Main);
        if (main.Get(1000) is { } bar)
        {
            WideDraw.Draw(target, bar, 0, 0, _wide, Extra);
        }

        var log = app.Log;
        var lamp = _pressed == Lamp ? (HistoryOpen ^ _over ? 1021 : 1020)
            : HistoryOpen ? 1021
            : log.Current is { } current ? 1023 + current.Priority
            : 0;
        if (lamp != 0 && (!log.Blinking || log.BlinkCount % 2 == 1) && main.Get(lamp) is { } lampPicture)
        {
            target.Draw(lampPicture, 497 + Extra, 3);
        }

        if (main.Get(_pressed == Back && _over ? 1010 : 1011) is { } back)
        {
            target.Draw(back, 601 + Extra, 0);
        }

        if (app.Session is { } session)
        {
            Assets.Fonts.ForIndex(0).Draw(target, session.Date, 588 + Extra, 12, 0, 2, 22, 0);
        }

        if (HistoryOpen && main.Get(1001) is { } drop)
        {
            WideDraw.Draw(target, drop, 11, 0, _wideDrop, Extra);
        }

        var font = Assets.Fonts.ForIndex(1);
        target.SetClip(11, 0, 437 - 25 - 11 + Extra, Height);
        for (var row = 0; row < (HistoryOpen ? Rows : 1); row++)
        {
            if (log.History[row] is not { } notice)
            {
                break;
            }

            var color = row == _hoveredRow ? (byte)0xFF : notice.Priority switch
            {
                1 => (byte)0x10,
                2 => (byte)0x12,
                _ => (byte)0x16,
            };
            font.Draw(target, notice.Text, TextLeft, FirstBaseline + (RowStep * row), 0, 0, color, 0);
        }
    }
}
