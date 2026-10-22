using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenPax.Forms.Drawing;
using OpenPax.Forms.Input;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms;

/// <summary>
/// Anything on a form: it measures and places itself in layout units,
/// draws, and takes the mouse and keys. Properties tell bindings when they
/// change. Controls a mod writes derive from this or from a control.
/// </summary>
public abstract class Element : INotifyPropertyChanged
{
    private Visibility _visibility;
    private bool _enabled = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The name a form finds the element by (x:Name or Name in markup).</summary>
    public string? Name { get; set; }

    public Element? Parent { get; internal set; }

    /// <summary>The form the element is on, once it is.</summary>
    public Form? Form => this as Form ?? Parent?.Form;

    /// <summary>A fixed size, or null for what the content needs (or the room given, when stretched).</summary>
    public int? Width { get; set; }

    public int? Height { get; set; }

    public int MinWidth { get; set; }

    public int MinHeight { get; set; }

    public int MaxWidth { get; set; } = Size.Infinite;

    public int MaxHeight { get; set; } = Size.Infinite;

    public Thickness Margin { get; set; }

    public HorizontalAlignment HorizontalAlignment { get; set; }

    public VerticalAlignment VerticalAlignment { get; set; }

    public Visibility Visibility
    {
        get => _visibility;
        set => Set(ref _visibility, value);
    }

    public bool IsEnabled
    {
        get => _enabled && (Parent?.IsEnabled ?? true);
        set => Set(ref _enabled, value);
    }

    /// <summary>A line shown while the mouse rests on the element.</summary>
    public string? ToolTip { get; set; }

    /// <summary>What bindings in this element and those inside it read from; inherited when not set.</summary>
    public object? DataContext
    {
        get => field ?? Parent?.DataContext;
        set => field = value;
    }

    /// <summary>Grid placement (attached properties in markup: Grid.Row and the rest).</summary>
    public int GridRow { get; set; }

    public int GridColumn { get; set; }

    public int GridRowSpan { get; set; } = 1;

    public int GridColumnSpan { get; set; } = 1;

    public Dock Dock { get; set; }

    /// <summary>Place in a Canvas panel.</summary>
    public int CanvasLeft { get; set; }

    public int CanvasTop { get; set; }

    /// <summary>What the element asked for at the last measure, margin included.</summary>
    public Size DesiredSize { get; private set; }

    /// <summary>Where the element was placed at the last arrange, margin excluded.</summary>
    public Rect Bounds { get; private set; }

    public bool IsMouseOver { get; internal set; }

    public bool IsPressed { get; internal set; }

    public bool IsFocused => Form?.Focused == this;

    /// <summary>Takes keyboard focus when clicked or tabbed to.</summary>
    public virtual bool Focusable => false;

    /// <summary>For an overlay, the element that opened it and closes it (a combo box for its list); null for none.</summary>
    public virtual Element? OverlayOwner => null;

    /// <summary>The elements inside, front last.</summary>
    public virtual IEnumerable<Element> Children => [];

    protected Theme Theme => Form?.Theme ?? throw new InvalidOperationException("The element is not on a form.");

    public Size Measure(Size available)
    {
        if (Visibility == Visibility.Collapsed)
        {
            DesiredSize = default;
            return DesiredSize;
        }

        var inner = Constrain(available.Deflate(Margin));
        var wanted = MeasureOverride(inner);
        var size = new Size(Width ?? Math.Clamp(wanted.Width, MinWidth, MaxWidth), Height ?? Math.Clamp(wanted.Height, MinHeight, MaxHeight));
        DesiredSize = new Size(Math.Min(size.Width, inner.Width), Math.Min(size.Height, inner.Height)).Inflate(Margin);
        return DesiredSize;
    }

    public void Arrange(Rect room)
    {
        if (Visibility == Visibility.Collapsed)
        {
            Bounds = room with { Width = 0, Height = 0 };
            return;
        }

        var r = room.Deflate(Margin);
        var want = DesiredSize.Deflate(Margin);
        var w = HorizontalAlignment == HorizontalAlignment.Stretch && Width is null ? r.Width : Math.Min(r.Width, want.Width);
        var h = VerticalAlignment == VerticalAlignment.Stretch && Height is null ? r.Height : Math.Min(r.Height, want.Height);
        w = Math.Clamp(w, Math.Min(MinWidth, r.Width), MaxWidth);
        h = Math.Clamp(h, Math.Min(MinHeight, r.Height), MaxHeight);
        var x = HorizontalAlignment switch
        {
            HorizontalAlignment.Center => r.X + (r.Width - w) / 2,
            HorizontalAlignment.Right => r.Right - w,
            _ => r.X,
        };
        var y = VerticalAlignment switch
        {
            VerticalAlignment.Center => r.Y + (r.Height - h) / 2,
            VerticalAlignment.Bottom => r.Bottom - h,
            _ => r.Y,
        };
        Bounds = new Rect(x, y, w, h);
        ArrangeOverride(Bounds);
    }

    public void Render(DrawContext dc)
    {
        if (Visibility != Visibility.Visible)
        {
            return;
        }

        var enabled = dc.Enabled;
        dc.Enabled = enabled && _enabled;
        OnRender(dc);
        RenderChildren(dc);
        dc.Enabled = enabled;
    }

    /// <summary>The deepest visible element under a point, this one included, or null.</summary>
    public virtual Element? HitTest(int x, int y)
    {
        if (Visibility != Visibility.Visible || !Bounds.Contains(x, y))
        {
            return null;
        }

        foreach (var child in Children.Reverse())
        {
            if (child.HitTest(x, y) is { } hit)
            {
                return hit;
            }
        }

        return this;
    }

    /// <summary>Finds a named element here or inside.</summary>
    public Element? Find(string name)
    {
        if (Name == name)
        {
            return this;
        }

        foreach (var child in Children)
        {
            if (child.Find(name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Every element here and inside, in tree order.</summary>
    public IEnumerable<Element> Descendants() => Children.SelectMany(c => c.Descendants().Prepend(c));

    protected virtual Size MeasureOverride(Size available)
    {
        var size = new Size(0, 0);
        foreach (var child in Children)
        {
            var d = child.Measure(available);
            size = new Size(Math.Max(size.Width, d.Width), Math.Max(size.Height, d.Height));
        }

        return size;
    }

    protected virtual void ArrangeOverride(Rect bounds)
    {
        foreach (var child in Children)
        {
            child.Arrange(bounds);
        }
    }

    protected virtual void OnRender(DrawContext dc)
    {
    }

    protected virtual void RenderChildren(DrawContext dc)
    {
        foreach (var child in Children)
        {
            child.Render(dc);
        }
    }

    protected internal virtual void OnMouseDown(int x, int y, bool right)
    {
    }

    /// <summary>The button let go; over is whether the mouse is still on the element.</summary>
    protected internal virtual void OnMouseUp(int x, int y, bool over)
    {
    }

    protected internal virtual void OnMouseMove(int x, int y, bool pressed)
    {
    }

    protected internal virtual void OnDoubleClick(int x, int y)
    {
    }

    /// <summary>The wheel turned; true when the element used it.</summary>
    protected internal virtual bool OnWheel(int delta) => false;

    /// <summary>A key; true when the element used it.</summary>
    protected internal virtual bool OnKey(FormKey key, bool shift) => false;

    protected internal virtual void OnText(char c)
    {
    }

    protected internal virtual void OnFocusChanged(bool focused)
    {
    }

    protected internal virtual void OnTick(long now)
    {
    }

    /// <summary>Sets a field and tells bindings when it changed.</summary>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Changed(property);
        return true;
    }

    protected void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    /// <summary>Joins a child to this element: its parent is set.</summary>
    protected T Adopt<T>(T child)
        where T : Element
    {
        child.Parent = this;
        return child;
    }

    private Size Constrain(Size s) =>
        new(Math.Min(Width ?? s.Width, Math.Min(s.Width, MaxWidth)), Math.Min(Height ?? s.Height, Math.Min(s.Height, MaxHeight)));
}
