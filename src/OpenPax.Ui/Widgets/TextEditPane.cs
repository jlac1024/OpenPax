using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 8017, an editable line of text (or, not editable, wrapped text).
/// The first click only gives it the keyboard and selects everything.
/// </summary>
public sealed class TextEditPane(ViewNode node, int left, int top, TextEditDetails details) : Pane(node, left, top)
{
    private const int BlinkPeriod = 333;
    private const byte CaretColor = 25;

    private int _anchor;
    private long _lastBlink;
    private bool _caretOn;
    private int _scroll;

    public TextEditDetails Details { get; } = details;

    public string Text { get; set; } = "";

    public bool Focused { get; private set; }

    public int Caret { get; private set; }

    public int SelectionStart { get; private set; }

    public int SelectionEnd { get; private set; }

    private bool Editable => Details.Editable != 0;

    private bool HasSelection => SelectionEnd > SelectionStart;

    private BitmapText Font => Assets.Fonts.ForIndex(Details.Font);

    protected internal override bool TakesFocus => true;

    protected internal override void FocusChanged(bool focused, long now)
    {
        Focused = focused;
        Caret = 0;
        (SelectionStart, SelectionEnd) = focused && Editable ? (0, Text.Length) : (0, 0);
        _caretOn = focused;
        _lastBlink = now;
    }

    protected internal override void MouseDown(int x, int y, int clicks)
    {
        if (!Focused || !Editable)
        {
            return;
        }

        if (clicks >= 2)
        {
            (SelectionStart, SelectionEnd) = (0, Text.Length);
            return;
        }

        Caret = _anchor = CharacterAt(x);
        (SelectionStart, SelectionEnd) = (0, 0);
    }

    protected internal override void MouseMove(int x, int y, long now)
    {
        if (Focused && Editable)
        {
            Caret = CharacterAt(x);
            (SelectionStart, SelectionEnd) = (Math.Min(_anchor, Caret), Math.Max(_anchor, Caret));
        }
    }

    // the first character whose prefix is wider than the point.
    private int CharacterAt(int x)
    {
        var local = x - Left + _scroll;
        for (var i = 1; i <= Text.Length; i++)
        {
            if (Font.Measure(Text[..i], Details.Style) > local)
            {
                return i - 1;
            }
        }

        return Text.Length;
    }

    protected internal override void Key(UiKey key, bool shift, long now)
    {
        if (!Enabled || !Editable)
        {
            return;
        }

        _caretOn = true;
        _lastBlink = now;
        switch (key)
        {
            case UiKey.Left or UiKey.Right or UiKey.Home or UiKey.End:
                var from = Caret;
                Caret = key switch
                {
                    UiKey.Left => Math.Max(0, Caret - 1),
                    UiKey.Right => Math.Min(Text.Length, Caret + 1),
                    UiKey.Home => 0,
                    _ => Text.Length,
                };
                if (shift)
                {
                    var anchor = HasSelection ? (from == SelectionStart ? SelectionEnd : SelectionStart) : from;
                    (SelectionStart, SelectionEnd) = (Math.Min(anchor, Caret), Math.Max(anchor, Caret));
                }
                else
                {
                    (SelectionStart, SelectionEnd) = (0, 0);
                }

                break;
            case UiKey.Backspace or UiKey.Delete:
                if (HasSelection)
                {
                    DeleteSelection();
                }
                else if (key == UiKey.Backspace && Caret > 0)
                {
                    Text = Text.Remove(Caret - 1, 1);
                    Caret--;
                }
                else if (key == UiKey.Delete && Caret < Text.Length)
                {
                    Text = Text.Remove(Caret, 1);
                }

                Post(UiMessage.TextChanged, 0);
                break;
            case UiKey.Return or UiKey.Tab:
                Post(UiMessage.TextDone, Id);
                Window?.SetFocus(null, now);
                break;
        }
    }

    protected internal override void Typed(char c, long now)
    {
        if (!Enabled || !Editable || c < 0x20 || Windows1252Code(c) is null)
        {
            return;
        }

        _caretOn = true;
        _lastBlink = now;
        if (HasSelection)
        {
            // A selection is replaced with no length check.
            DeleteSelection();
        }
        else if (Text.Length == Details.MaxLength)
        {
            // Quirk kept: the test is "not equal", so only exactly full stops typing.
            return;
        }

        Text = Text.Insert(Caret, c.ToString());
        Caret++;
        Post(UiMessage.TextChanged, Id);
    }

    private static int? Windows1252Code(char c)
    {
        return OpenPax.Data.Text.Windows1252.GetByte(c);
    }

    private void DeleteSelection()
    {
        Text = Text.Remove(SelectionStart, SelectionEnd - SelectionStart);
        Caret = SelectionStart;
        (SelectionStart, SelectionEnd) = (0, 0);
    }

    protected internal override void Tick(long now)
    {
        if (Focused && Shown && Enabled && now - _lastBlink >= BlinkPeriod)
        {
            _caretOn = !_caretOn;
            _lastBlink = now;
        }
    }

    protected override void Draw(Framebuffer target)
    {
        var font = Font;
        if (!Editable)
        {
            WrappedText.Draw(target, font, Text, Left, Top, Width, Height, Details.Style, Details.Align, Details.Color, Details.ShadowColor);
            return;
        }

        // Keep the caret between 1 and width - 3 pixels from the left edge.
        var caretX = font.Measure(Text[..Caret], Details.Style);
        if (caretX - _scroll > Width - 3)
        {
            _scroll = caretX - (Width - 3);
        }
        else if (caretX - _scroll < 1)
        {
            _scroll = Math.Max(0, caretX - 1);
        }

        var baseline = TextPlacement.Baseline(font, Details.Style, Top + Height);
        if (HasSelection)
        {
            // Provisional: the selection colors are not read yet.
            var x0 = font.Measure(Text[..SelectionStart], Details.Style);
            var x1 = font.Measure(Text[..SelectionEnd], Details.Style);
            target.FillRect(Left + x0 - _scroll, Top + Height - font.LineHeight, x1 - x0, font.LineHeight, CaretColor);
        }

        font.Draw(target, Text, Left - _scroll, baseline, Details.Style, 0, Details.Color, Details.ShadowColor);
        if (Focused && _caretOn && !HasSelection)
        {
            var x = Math.Min(Left + caretX - _scroll, Left + Width - 1);
            target.FillRect(x, Top + Height - font.LineHeight, 1, font.LineHeight, CaretColor);
        }
    }
}
