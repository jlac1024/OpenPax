using OpenPax.Forms.Drawing;
using OpenPax.Forms.Layout;
using OpenPax.Render.TrueColor;

namespace OpenPax.Forms.Controls;

/// <summary>An element holding any number of others, which it lays out its own way.</summary>
public abstract class Panel : Element
{
    private readonly List<Element> _items = [];

    public IReadOnlyList<Element> Items => _items;

    /// <summary>A color filled behind the children, when set ("#RRGGBB" in markup).</summary>
    public Color? Background { get; set; }

    public override IEnumerable<Element> Children => _items;

    public void Add(Element child) => _items.Add(Adopt(child));

    public void Remove(Element child)
    {
        _items.Remove(child);
        child.Parent = null;
    }

    public void ClearItems()
    {
        foreach (var c in _items)
        {
            c.Parent = null;
        }

        _items.Clear();
    }

    protected override void OnRender(DrawContext dc)
    {
        if (Background is { } b)
        {
            dc.Fill(Bounds, b);
        }
    }

    protected IEnumerable<Element> Shown => _items.Where(c => c.Visibility != Visibility.Collapsed);
}
