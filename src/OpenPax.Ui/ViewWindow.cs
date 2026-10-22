using OpenPax.Data.Resources;
using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui;

/// <summary>
/// A window built from its VIEW layout: a background picture from
/// Main.img, then every widget. It routes the mouse, the keyboard and time
/// to the widgets, and queues what they report for its screen to handle.
/// </summary>
public sealed class ViewWindow
{
    private readonly Dictionary<int, Pane> _byId = [];
    private readonly Queue<UiMessage> _messages = [];
    private Pane? _hover;
    private Pane? _held;
    private Pane? _focus;
    private int _mouseX;
    private int _mouseY;

    /// <param name="background">The Main.img picture behind the window; most windows use their own id, not all.</param>
    /// <param name="custom">Makes the panes for a screen's own widget classes; null for the rest.</param>
    /// <param name="offsetX">Where the window's corner goes on screen; popups are moved there.</param>
    // (window constructor: view id, palette index, parent)
    public ViewWindow(UiAssets assets, int viewId, int paletteIndex, int background, Func<ViewNode, int, int, Pane?>? custom = null, int offsetX = 0, int offsetY = 0)
    {
        Assets = assets;
        Layout = ViewReader.Parse(assets.Views.Get(ResourceType.View, viewId).Span);
        PaletteIndex = paletteIndex;
        Background = background;
        Root = Build(Layout, offsetX, offsetY, offsetX, offsetY, custom);
    }

    /// <summary>A window from a layout made in code: the port's own screens, built from the game's widgets and art.</summary>
    public ViewWindow(UiAssets assets, ViewNode layout, int paletteIndex, int background, Func<ViewNode, int, int, Pane?>? custom = null)
    {
        Assets = assets;
        Layout = layout;
        PaletteIndex = paletteIndex;
        Background = background;
        Root = Build(Layout, 0, 0, 0, 0, custom);
    }

    public UiAssets Assets { get; }

    public ViewNode Layout { get; }

    /// <summary>The palette; the Planetary Surface window changes it with the planet shown.</summary>
    public int PaletteIndex { get; set; }

    public int Background { get; }

    public Pane Root { get; }

    /// <summary>How much wider than the original's 640 the window is drawn (docs/ui/widescreen.md); 0 for its own size.</summary>
    public int Extra { get; private set; }

    /// <summary>Panes this short keep their height when a row span crosses them.</summary>
    private const int FixedHeight = 24;

    /// <summary>How much taller than the original's 480 the window is drawn; 0 for its own size.</summary>
    public int ExtraHeight { get; private set; }

    /// <summary>How the window's art and layout grow; null for not at all.</summary>
    public WideLayout? Stretch { get; private set; }

    /// <summary>
    /// Lays the window out larger by some amount across and down at the
    /// layout's spans: a pane moves by the shares of the spans before it and
    /// grows by those of the spans it crosses. A button keeps its height and
    /// moves down by a row span's share when its middle lies past the span's
    /// middle (its picture would not stretch well), as does anything as short
    /// as a line of text (a spinner, a label). Every pane is placed from
    /// where the layout first put it, so growing again or back is exact.
    /// </summary>
    public void Widen(int extra, int extraHeight, WideLayout layout)
    {
        (Extra, ExtraHeight, Stretch) = (Math.Max(0, extra), Math.Max(0, extraHeight), layout);
        var rowShares = layout.RowShares(ExtraHeight);
        foreach (var pane in Panes(Root))
        {
            var o = pane.Original ??= (pane.Left, pane.Top, pane.Width, pane.Height);
            var (shift, grow) = layout.For(o.Left - Root.Left, o.Width, Extra);
            var top = o.Top - Root.Top;
            var (down, taller) = layout.ForRows(top, o.Height, ExtraHeight);
            if ((pane is ButtonPane || o.Height <= FixedHeight) && taller > 0)
            {
                (down, taller) = (0, 0);
                for (var i = 0; i < layout.Rows.Count; i++)
                {
                    var (row, span) = layout.Rows[i];
                    down += row + span <= top || (row < top + o.Height && top + (o.Height / 2) >= row + (span / 2)) ? rowShares[i] : 0;
                }
            }

            if (grow > 0 || taller > 0 || pane.Resized)
            {
                pane.Place(o.Left + shift, o.Top + down, o.Width + grow, o.Height + taller);
                pane.Resized = true;
            }
            else
            {
                pane.MoveTo(o.Left + shift, o.Top + down);
            }
        }
    }

    /// <summary>Where a rectangle across of the original layout lies now (a list's heading cell): moved and grown as a pane there would be.</summary>
    public (int Left, int Width) Map(int left, int width)
    {
        if (Stretch is not { } layout || Extra == 0)
        {
            return (left, width);
        }

        var (shift, grow) = layout.For(left - Root.Left, width, Extra);
        return (left + shift, width + grow);
    }

    private static IEnumerable<Pane> Panes(Pane pane)
    {
        yield return pane;
        foreach (var child in pane.Children.ToList())
        {
            foreach (var p in Panes(child))
            {
                yield return p;
            }
        }
    }

    /// <summary>
    /// The press being handled is the right button. The original turns a
    /// right press into a left one with event flag bit 4 set.
    /// </summary>
    public bool RightButton { get; private set; }

    /// <summary>The first element with this id, as the original's lookup returns the first match.</summary>
    public Pane Get(int id)
    {
        return _byId.TryGetValue(id, out var w) ? w : throw new KeyNotFoundException(id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public T Get<T>(int id)
        where T : Pane
    {
        return (T)Get(id);
    }

    /// <summary>Adds a pane over the rest, drawn last and hit first.</summary>
    public void AddOverlay(Pane pane)
    {
        pane.Window = this;
        Root.Add(pane);
    }

    internal void Post(UiMessage message)
    {
        _messages.Enqueue(message);
    }

    /// <summary>What the widgets reported since the last call, oldest first.</summary>
    public IEnumerable<UiMessage> TakeMessages()
    {
        while (_messages.TryDequeue(out var m))
        {
            yield return m;
        }
    }

    public void Draw(Framebuffer target)
    {
        if (Assets.Pictures(ImageSet.Main).Get(Background) is { } picture)
        {
            if (Stretch is { } layout && (Extra > 0 || ExtraHeight > 0))
            {
                WideDraw.Draw(target, picture, Root.Left, Root.Top, layout, Extra, ExtraHeight);
            }
            else
            {
                target.Draw(picture, Root.Left, Root.Top);
            }
        }

        foreach (var child in Root.Children)
        {
            child.DrawTree(target);
        }
    }

    /// <summary>
    /// A press goes to the deepest widget under the pointer, which keeps the
    /// mouse until release. A widget that takes the keyboard
    /// only gets it on its first click.
    /// </summary>
    public void MouseDown(int x, int y, int clicks, long now, bool right = false)
    {
        (_mouseX, _mouseY) = (x, y);
        RightButton = right;
        _held = HitTest(Root, x, y);
        if (_held is null)
        {
            SetFocus(null, now);
            return;
        }

        if (_held.TakesFocus && _focus != _held)
        {
            SetFocus(_held, now);
            return;
        }

        if (!_held.TakesFocus)
        {
            SetFocus(null, now);
        }

        _held.MouseDown(x, y, clicks);
    }

    public void MouseMove(int x, int y, long now)
    {
        (_mouseX, _mouseY) = (x, y);
        _held?.MouseMove(x, y, now);
    }

    public void MouseUp(int x, int y)
    {
        (_mouseX, _mouseY) = (x, y);
        var held = _held;
        _held = null;
        held?.MouseUp(x, y);
    }

    public void Key(UiKey key, bool shift, long now)
    {
        _focus?.Key(key, shift, now);
    }

    public void Typed(string text, long now)
    {
        foreach (var c in text)
        {
            _focus?.Typed(c, now);
        }
    }

    /// <summary>
    /// Once a frame: the widget under the pointer hears it is there
    /// , a held widget hears the pointer again so held arrows
    /// repeat, and everything gets the time.
    /// </summary>
    public void Tick(long now)
    {
        var over = HitTest(Root, _mouseX, _mouseY);
        if (over != _hover)
        {
            _hover?.MouseLeft();
            _hover = over;
        }

        _hover?.MouseWithin(_mouseX, _mouseY);
        _held?.MouseMove(_mouseX, _mouseY, now);
        TickTree(Root, now);
    }

    /// <summary>A widget has the keyboard (a text field being typed in).</summary>
    public bool HasFocus => _focus is not null;

    public void SetFocus(Pane? pane, long now)
    {
        if (_focus == pane)
        {
            return;
        }

        _focus?.FocusChanged(false, now);
        _focus = pane;
        _focus?.FocusChanged(true, now);
    }

    private static void TickTree(Pane pane, long now)
    {
        pane.Tick(now);
        foreach (var child in pane.Children)
        {
            TickTree(child, now);
        }
    }

    /// <summary>Later children are drawn on top, so they are tested first.</summary>
    private static Pane? HitTest(Pane parent, int x, int y)
    {
        for (var i = parent.Children.Count - 1; i >= 0; i--)
        {
            var child = parent.Children[i];
            if (child.Children.Count > 0 && child.Visible && HitTest(child, x, y) is { } inner)
            {
                return inner;
            }

            if (child.HitTest(x, y))
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// Widgets inside a group are placed in window coordinates, not the
    /// group's: in VIEW 31000 the group 31108 is at (381, 97) and its
    /// buttons at (384, 276), inside it.
    /// </summary>
    private Pane Build(ViewNode node, int originLeft, int originTop, int windowLeft, int windowTop, Func<ViewNode, int, int, Pane?>? custom)
    {
        var left = originLeft + node.Base.Left;
        var top = originTop + node.Base.Top;
        if (node.ClassId == ViewReader.WindowClass)
        {
            (windowLeft, windowTop) = (left, top);
        }

        var pane = custom?.Invoke(node, left, top) ?? Make(node, left, top);
        pane.Window = this;
        foreach (var child in node.Children)
        {
            pane.Add(Build(child, windowLeft, windowTop, windowLeft, windowTop, custom));
        }

        _byId.TryAdd(node.Base.Id, pane);
        return pane;
    }

    private static Pane Make(ViewNode node, int left, int top)
    {
        return node.Details switch
        {
            ButtonDetails d => new ButtonPane(node, left, top, d),
            LabelDetails d => new LabelPane(node, left, top, d),
            PictureDetails d => new PicturePane(node, left, top, d),
            CheckBoxDetails d => new CheckBoxPane(node, left, top, d),
            StateBoxDetails d => new StateBoxPane(node, left, top, d),
            EmpireToggleDetails d => new EmpireTogglePane(node, left, top, d),
            VolumeSliderDetails d => new VolumeSliderPane(node, left, top, d),
            HotSpotDetails d => new HotSpotPane(node, left, top, d),
            TextEditDetails d => new TextEditPane(node, left, top, d),
            SpinnerDetails d => new SpinnerPane(node, left, top, d),
            ListBoxDetails d => new ListBoxPane(node, left, top, d),
            HeaderBarDetails d => new HeaderBarPane(node, left, top, d),
            ScrollBarDetails d => new ScrollBarPane(node, left, top, d),
            AnimationDetails d => new AnimationPane(node, left, top, d),
            TextFieldDetails d when node.ClassId == 8024 => new WrappedTextPane(node, left, top, d),
            TextFieldDetails d => new TextLinePane(node, left, top, d),
            _ => new Pane(node, left, top),
        };
    }
}
