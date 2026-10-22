using OpenPax.Forms.Drawing;
using OpenPax.Forms.Input;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms;

/// <summary>
/// A form: the root of a tree of elements, usually read from markup. It
/// keeps keyboard focus, the overlays that float over everything (a combo
/// box's list, a tooltip), and routes the mouse and keys.
/// </summary>
public class Form : Element
{
    private const int ToolTipDelay = 600;
    private const int DoubleClickTime = 400;

    private readonly List<Element> _overlays = [];
    private Element? _content;
    private Element? _hover;
    private Element? _captured;
    private long _now;
    private long _hoverSince;
    private (int X, int Y) _mouse;
    private long _lastClick = long.MinValue / 2;
    private Element? _lastClicked;

    /// <summary>The form's one child; panels hold the rest.</summary>
    public Element? Content
    {
        get => _content;
        set
        {
            _content = value is null ? null : Adopt(value);
        }
    }

    /// <summary>Set by the host before the form is laid out.</summary>
    public new Theme Theme { get; set; } = null!; // Set by FormHost.Show before any use.

    /// <summary>Draws the panel background under the content; off for forms laid over a picture.</summary>
    public bool Background { get; set; } = true;

    /// <summary>The form's code-behind, whose methods markup names as event handlers.</summary>
    public object? CodeBehind { get; set; }

    public Element? Focused { get; private set; }

    /// <summary>The form's bindings, made by the markup reader.</summary>
    public List<Markup.Binding> Bindings { get; } = [];

    /// <summary>Sets what the bindings read and fills every bound property from it.</summary>
    public void Bind(object? dataContext)
    {
        DataContext = dataContext;
        foreach (var b in Bindings)
        {
            b.Refresh();
        }
    }

    /// <summary>A named element of a type, or an exception naming what is missing.</summary>
    public T Get<T>(string name)
        where T : Element =>
        Find(name) as T ?? throw new KeyNotFoundException(name);

    public override IEnumerable<Element> Children => _content is null ? [] : [_content];

    /// <summary>Lays an element over everything, placed in form units (a drop-down list).</summary>
    public void ShowOverlay(Element overlay, Rect where)
    {
        Adopt(overlay);
        _overlays.Remove(overlay);
        _overlays.Add(overlay);
        overlay.Measure(new Size(where.Width, where.Height));
        overlay.Arrange(where);
    }

    public void CloseOverlay(Element overlay) => _overlays.Remove(overlay);

    public bool HasOverlay(Element overlay) => _overlays.Contains(overlay);

    public void Focus(Element? element)
    {
        if (Focused == element)
        {
            return;
        }

        var old = Focused;
        Focused = element;
        old?.OnFocusChanged(false);
        element?.OnFocusChanged(true);
    }

    /// <summary>Lays the form out over a size and draws it, overlays last.</summary>
    public void Layout(int width, int height)
    {
        Measure(new Size(width, height));
        Arrange(new Rect(0, 0, width, height));
    }

    public void Draw(DrawContext dc)
    {
        if (Background)
        {
            dc.Backdrop(Bounds);
        }

        Render(dc);
        foreach (var overlay in _overlays)
        {
            overlay.Render(dc);
        }

        if (_hover?.ToolTip is { Length: > 0 } tip && _captured is null && _now - _hoverSince >= ToolTipDelay)
        {
            var font = Theme.DefaultFont;
            var w = dc.Measure(tip, font) + 6;
            var h = dc.LineHeight(font) + 4;
            var x = Math.Min(_mouse.X + 8, Bounds.Right - w);
            var y = Math.Min(_mouse.Y + 14, Bounds.Bottom - h);
            var r = new Rect(x, y, w, h);
            dc.Fill(r, Theme.Well);
            dc.Outline(r, Theme.Accent);
            dc.Text(tip, x + 3, y + 2, Theme.Text, font);
        }
    }

    public void Tick(long now)
    {
        _now = now;
        foreach (var e in Descendants().Concat(_overlays.SelectMany(o => o.Descendants().Prepend(o))).ToList())
        {
            e.OnTick(now);
        }
    }

    /// <summary>The element under a point, overlays first; null where only the bare form is.</summary>
    public Element? ElementAt(int x, int y)
    {
        for (var i = _overlays.Count - 1; i >= 0; i--)
        {
            if (_overlays[i].HitTest(x, y) is { } hit)
            {
                return hit;
            }
        }

        var found = HitTest(x, y);
        return found == this ? null : found;
    }

    /// <summary>The mouse moved; true when it is over something of this form's.</summary>
    public bool MouseMove(int x, int y)
    {
        _mouse = (x, y);
        var over = ElementAt(x, y);
        if (over != _hover)
        {
            if (_hover is not null)
            {
                _hover.IsMouseOver = false;
            }

            _hover = over;
            _hoverSince = _now;
            if (over is not null)
            {
                over.IsMouseOver = true;
            }
        }

        _captured?.OnMouseMove(x, y, true);
        if (_captured is null)
        {
            over?.OnMouseMove(x, y, false);
        }

        return over is not null || Background;
    }

    public bool MouseDown(int x, int y, bool right)
    {
        MouseMove(x, y);
        var target = ElementAt(x, y);

        // A click outside an open overlay closes it, except on the control
        // that opened it, which closes it itself (a combo box's second click).
        foreach (var overlay in _overlays.ToList())
        {
            if (target is null || (!IsInside(target, overlay) && target != overlay.OverlayOwner))
            {
                _overlays.Remove(overlay);
            }
        }

        if (target is null || !target.IsEnabled)
        {
            return target is not null || Background;
        }

        Focus(FocusTarget(target));
        _captured = target;
        target.IsPressed = true;
        if (target == _lastClicked && _now - _lastClick <= DoubleClickTime)
        {
            target.OnDoubleClick(x, y);
            _lastClicked = null;
        }
        else
        {
            target.OnMouseDown(x, y, right);
            (_lastClicked, _lastClick) = (target, _now);
        }

        return true;
    }

    public bool MouseUp(int x, int y)
    {
        if (_captured is not { } captured)
        {
            return ElementAt(x, y) is not null || Background;
        }

        _captured = null;
        captured.IsPressed = false;
        captured.OnMouseUp(x, y, captured.Bounds.Contains(x, y));
        return true;
    }

    public bool Wheel(int delta)
    {
        for (var e = ElementAt(_mouse.X, _mouse.Y); e is not null; e = e.Parent)
        {
            if (e.OnWheel(delta))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A key: the focused element first, then Tab moves the focus.</summary>
    public bool Key(FormKey key, bool shift)
    {
        if (Focused is { } f && f.IsEnabled && f.OnKey(key, shift))
        {
            return true;
        }

        if (key == FormKey.Tab)
        {
            var order = Descendants().Where(e => e.Focusable && e.IsEnabled && e.Visibility == Visibility.Visible).ToList();
            if (order.Count > 0)
            {
                var i = Focused is null ? -1 : order.IndexOf(Focused);
                Focus(order[((i + (shift ? -1 : 1)) % order.Count + order.Count) % order.Count]);
            }

            return true;
        }

        return false;
    }

    public bool Text(char c)
    {
        if (Focused is { } f && f.IsEnabled)
        {
            f.OnText(c);
            return true;
        }

        return false;
    }

    private static bool IsInside(Element e, Element container)
    {
        for (var p = e; p is not null; p = p.Parent)
        {
            if (p == container)
            {
                return true;
            }
        }

        return false;
    }

    private static Element? FocusTarget(Element e)
    {
        for (var p = e; p is not null; p = p.Parent)
        {
            if (p.Focusable)
            {
                return p;
            }
        }

        return null;
    }
}
