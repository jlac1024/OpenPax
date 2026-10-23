using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// A window element at run time (the original's pane): its
/// layout from the VIEW data, where it sits on screen, and the state that
/// changes. Each widget class draws and takes input in its own subclass.
/// </summary>
public class Pane
{
    private readonly List<Pane> _children = [];

    public Pane(ViewNode node, int screenLeft, int screenTop)
    {
        Node = node;
        Left = screenLeft;
        Top = screenTop;
        Visible = node.Base.Visible != 0;
        Enabled = node.Base.Enabled != 0;
    }

    public ViewNode Node { get; }

    public int Id => Node.Base.Id;

    private int? _width;
    private int? _height;

    /// <summary>Screen position (the layout's is relative to the window).</summary>
    public int Left { get; private set; }

    public int Top { get; private set; }

    public virtual int Width => _width ?? Node.Base.Width;

    public virtual int Height => _height ?? Node.Base.Height;

    /// <summary>Where the pane was before a wide window moved or widened it (docs/ui/widescreen.md); null until then.</summary>
    public (int Left, int Top, int Width, int Height)? Original { get; internal set; }

    /// <summary>A wide window has resized the pane, so going back must put its own size back too.</summary>
    internal bool Resized { get; set; }

    /// <summary>Moves the pane, its size left as it is; its children stay where they are.</summary>
    public void MoveTo(int left, int top)
    {
        (Left, Top) = (left, top);
    }

    /// <summary>Moves and resizes the pane; its children stay where they are.</summary>
    public void Place(int left, int top, int width, int height)
    {
        (Left, Top, _width, _height) = (left, top, width, height);
    }

    public Pane? Parent { get; private set; }

    public IReadOnlyList<Pane> Children => _children;

    public bool Visible { get; set; }

    public bool Enabled { get; set; }

    /// <summary>The window it belongs to, set when the window is built.</summary>
    protected internal ViewWindow? Window { get; internal set; }

    protected UiAssets Assets => Window?.Assets ?? throw new InvalidOperationException("The pane is not in a window yet.");

    /// <summary>Shown, and every container above it shown.</summary>
    public bool Shown => Visible && (Parent?.Shown ?? true);

    internal void Add(Pane child)
    {
        child.Parent = this;
        _children.Add(child);
    }

    /// <summary>
    /// Whether a point hits the pane: inside its rect (edges included, as
    /// tests) while shown and enabled.
    /// </summary>
    public virtual bool HitTest(int x, int y)
    {
        return Shown && Enabled && Inside(x, y);
    }

    public bool Inside(int x, int y)
    {
        return x >= Left && x <= Left + Width && y >= Top && y <= Top + Height;
    }

    /// <summary>Draws the pane and its children, each clipped to its own rect.</summary>
    internal void DrawTree(Framebuffer target)
    {
        if (!Visible)
        {
            return;
        }

        target.SetClip(Left, Top, Width, Height);
        Draw(target);
        foreach (var child in InPaintOrder(_children))
        {
            child.DrawTree(target);
        }

        target.ResetClip();
    }

    /// <summary>
    /// Children in drawing order. The original draws a direct-refresh
    /// animation to the screen only when its picture changes,
    /// and it stays until something draws over it; this port draws every
    /// pane each frame. So those animations go in the order their pictures
    /// last changed, in the places they hold among their siblings: where
    /// they overlap, the newest picture is on top, as on the original's
    /// screen (the species lineup's frames each hold part of the next tube).
    /// </summary>
    internal static IEnumerable<Pane> InPaintOrder(IReadOnlyList<Pane> children)
    {
        var direct = children.OfType<AnimationPane>().Where(a => a.Details.DirectRefresh != 0).ToList();
        if (direct.Count < 2)
        {
            return children;
        }

        using var newest = direct.OrderBy(a => a.Painted).GetEnumerator();
        var ordered = new List<Pane>(children.Count);
        foreach (var child in children)
        {
            if (child is AnimationPane { Details.DirectRefresh: not 0 } && newest.MoveNext())
            {
                ordered.Add(newest.Current);
            }
            else
            {
                ordered.Add(child);
            }
        }

        return ordered;
    }

    /// <summary>The pane's own drawing (slot 0x84). The base draws the layout's background picture, if any.</summary>
    protected virtual void Draw(Framebuffer target)
    {
        if (Node.BackgroundPicture is > 0 and var picture && Assets.Pictures(ImageSet.Main).Get(picture) is { } sprite)
        {
            DrawFitted(target, sprite);
        }
    }

    /// <summary>
    /// A picture at the pane's corner; when a large window has grown the pane
    /// and the picture filled it (a panel's art), stretched where the
    /// window's spans cross it, as the window's own art is (docs/ui/widescreen.md).
    /// </summary>
    protected void DrawFitted(Framebuffer target, Sprite sprite)
    {
        if ((Grown > 0 || GrownHeight > 0) && Window?.Stretch is { } layout && Original is { } o && sprite.Width >= o.Width)
        {
            var (originLeft, originTop) = (o.Left - Window.Root.Left, o.Top - Window.Root.Top);
            var local = new WideLayout(
                [.. layout.Spans.Where(s => s.Column < originLeft + o.Width && s.Column + s.Span > originLeft).Select(s => (Math.Max(0, s.Column - originLeft), s.Span))],
                [.. layout.Tiled.Select(t => (t.Top - originTop, t.Bottom - originTop, t.Period))],
                [.. layout.Rows.Where(r => r.Row < originTop + o.Height && r.Row + r.Span > originTop).Select(r => (Math.Max(0, r.Row - originTop), r.Span))],
                RepeatRows: layout.RepeatRows);
            WideDraw.Draw(target, sprite, Left, Top, local, Grown, GrownHeight);
        }
        else
        {
            target.Draw(sprite, Left, Top);
        }
    }

    /// <summary>How much wider than the original layout a wide window has made the pane.</summary>
    protected int Grown => Original is { } o ? Math.Max(0, Width - o.Width) : 0;

    /// <summary>How much taller.</summary>
    protected int GrownHeight => Original is { } o ? Math.Max(0, Height - o.Height) : 0;

    /// <summary>Every frame while the pointer is over the pane (slot 0x3C).</summary>
    protected internal virtual void MouseWithin(int x, int y)
    {
    }

    /// <summary>The pointer moved off the pane (slot 0x40).</summary>
    protected internal virtual void MouseLeft()
    {
    }

    protected internal virtual void MouseDown(int x, int y, int clicks)
    {
    }

    /// <summary>The pointer moved while the button is held after a press on this pane (slot 0x48).</summary>
    protected internal virtual void MouseMove(int x, int y, long now)
    {
    }

    protected internal virtual void MouseUp(int x, int y)
    {
    }

    /// <summary>Time passes; panes that animate or blink do it here. Milliseconds from any fixed start.</summary>
    protected internal virtual void Tick(long now)
    {
    }

    /// <summary>Whether the pane takes the keyboard when clicked.</summary>
    protected internal virtual bool TakesFocus => false;

    protected internal virtual void FocusChanged(bool focused, long now)
    {
    }

    protected internal virtual void Key(UiKey key, bool shift, long now)
    {
    }

    protected internal virtual void Typed(char c, long now)
    {
    }

    /// <summary>Plays a sound by id; 0 is no sound.</summary>
    protected void PlaySound(int id)
    {
        if (id != 0)
        {
            Window?.Assets.Sound.Play(id);
        }
    }

    protected void Post(int message, int value)
    {
        Window?.Post(new UiMessage(message, value));
    }
}
