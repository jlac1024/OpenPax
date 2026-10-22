using OpenPax.Forms.Drawing;
using OpenPax.Forms.Input;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>The picked item in a field strip with the game's down arrow; clicking opens the items in a list over everything below it.</summary>
public sealed class ComboBox : ItemsControl
{
    private const int MaxRows = 8;

    private DropList? _drop;

    public int? Font { get; set; }

    public override bool Focusable => true;

    public bool IsDropDownOpen => _drop is not null && Form?.HasOverlay(_drop) == true;

    private int FontIndex => Font ?? Theme.DefaultFont;

    protected override Size MeasureOverride(Size available)
    {
        var widest = Items.Count == 0 ? 40 : Items.Max(i => Theme.Measure(FontIndex, TextOf(i)));
        return new Size(widest + Theme.FieldTextInset + ArrowRoom, Math.Max(18, Theme.LineHeight(FontIndex) + 6));
    }

    private const int ArrowWidth = 14;

    /// <summary>What the arrow takes at the right: the end cap, a gap each side, and the arrow.</summary>
    private int ArrowRoom => (Theme.Field?.Right ?? 2) + 4 + ArrowWidth + 4;

    protected override void OnRender(DrawContext dc)
    {
        dc.SkinOr(Theme.Field, Bounds, Theme.Well, true);
        var h = dc.LineHeight(FontIndex);
        dc.PushClip(Bounds.Deflate(new Thickness(Theme.FieldTextInset, 0, ArrowRoom, 0)));
        dc.Text(TextOf(SelectedItem), Bounds.X + Theme.FieldTextInset, Bounds.Y + (Bounds.Height - h) / 2, Theme.Text, FontIndex);
        dc.PopClip();
        // The arrow stands clear of the field's right end cap, as the text does of the left.
        var arrow = new Rect(Bounds.Right - (Theme.Field?.Right ?? 2) - 4 - ArrowWidth, Bounds.Y, ArrowWidth, Bounds.Height);
        if (Theme.ArrowDown is { } down)
        {
            dc.ImageCentered(down, arrow);
        }
        else
        {
            dc.Fill(new Rect(arrow.X + 3, arrow.Y + Bounds.Height / 2 - 1, 7, 2), Theme.Accent);
        }

        if (IsFocused)
        {
            dc.Outline(Bounds, Theme.Accent.WithAlpha(120));
        }
    }

    protected internal override void OnMouseDown(int x, int y, bool right)
    {
        if (IsDropDownOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    /// <summary>A quick second click opens or closes the list as a single one does.</summary>
    protected internal override void OnDoubleClick(int x, int y) => OnMouseDown(x, y, false);

    protected internal override bool OnKey(FormKey key, bool shift)
    {
        switch (key)
        {
            case FormKey.Up when Items.Count > 0:
                SelectedIndex = Math.Max(0, SelectedIndex - 1);
                return true;
            case FormKey.Down when Items.Count > 0:
                SelectedIndex = Math.Min(Items.Count - 1, SelectedIndex + 1);
                return true;
            case FormKey.Space or FormKey.Return:
                if (IsDropDownOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }

                return true;
            case FormKey.Escape when IsDropDownOpen:
                Close();
                return true;
            default:
                return false;
        }
    }

    internal void Picked(int index)
    {
        SelectedIndex = index;
        Close();
    }

    private void Open()
    {
        if (Form is not { } form || Items.Count == 0)
        {
            return;
        }

        _drop = new DropList(this) { Font = Font, DisplayMemberPath = DisplayMemberPath };
        _drop.Items.AddRange(Items);
        _drop.SelectedIndex = SelectedIndex;
        var rowHeight = Theme.LineHeight(FontIndex) + 2;
        var height = Math.Min(Items.Count, MaxRows) * rowHeight + 6;
        var below = Bounds.Bottom + height <= form.Bounds.Bottom;
        form.ShowOverlay(_drop, new Rect(Bounds.X, below ? Bounds.Bottom : Bounds.Y - height, Bounds.Width, height));
    }

    private void Close()
    {
        if (_drop is not null)
        {
            Form?.CloseOverlay(_drop);
            _drop = null;
        }
    }
}
