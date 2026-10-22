using OpenPax.Forms.Drawing;
using OpenPax.Forms.Input;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>
/// A line of text to type, in the game's clamped field strip. Return fires
/// Submitted. Entering the box (a click or Tab) selects all its text, so
/// typing replaces it; a click inside a box already entered, or an arrow
/// key, places the caret instead.
/// </summary>
public sealed class TextBox : Element
{
    private const int BlinkTime = 500;

    private string _text = "";
    private int _caret;
    private int _scroll;
    private long _now;
    private long _focusedAt;
    private bool _all;
    private bool _focusedByClick;

    public string Text
    {
        get => _text;
        set
        {
            if (Set(ref _text, (value ?? "").Length > MaxLength ? (value ?? "")[..MaxLength] : value ?? ""))
            {
                _caret = Math.Min(_caret, _text.Length);
                if (!IsFocused)
                {
                    _all = false;
                }

                TextChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public int MaxLength { get; set; } = 200;

    /// <summary>Shows stars in place of the letters.</summary>
    public bool IsPassword { get; set; }

    /// <summary>Text shown dim while the box is empty.</summary>
    public string Placeholder { get; set; } = "";

    public int? Font { get; set; }

    public int Caret => _caret;

    /// <summary>The whole text is selected: typing replaces it, Backspace or Delete clears it.</summary>
    public bool AllSelected => _all && _text.Length > 0;

    public override bool Focusable => true;

    public event EventHandler? TextChanged;

    public event EventHandler? Submitted;

    private int FontIndex => Font ?? Theme.DefaultFont;

    private string Shown => IsPassword ? new string('*', _text.Length) : _text;

    protected override Size MeasureOverride(Size available) => new(80, Math.Max(18, Theme.LineHeight(FontIndex) + 6));

    protected override void OnRender(DrawContext dc)
    {
        dc.SkinOr(Theme.Field, Bounds, Theme.Well, true);
        var inner = Bounds.Deflate(new Thickness(Theme.FieldTextInset, 2, Theme.FieldTextInset - 2, 2));
        var caretX = dc.Measure(Shown[.._caret], FontIndex);
        if (caretX - _scroll > inner.Width - 2)
        {
            _scroll = caretX - inner.Width + 2;
        }
        else if (caretX - _scroll < 0)
        {
            _scroll = Math.Max(0, caretX);
        }

        var h = dc.LineHeight(FontIndex);
        var y = inner.Y + (inner.Height - h) / 2;
        dc.PushClip(inner);
        if (IsFocused && AllSelected)
        {
            dc.Fill(new Rect(inner.X - _scroll, y, dc.Measure(Shown, FontIndex), h), Theme.Selection);
        }

        if (_text.Length == 0 && !IsFocused)
        {
            dc.Text(Placeholder, inner.X, y, Theme.DimText, FontIndex);
        }
        else
        {
            dc.Text(Shown, inner.X - _scroll, y, Theme.Text, FontIndex);
        }

        if (IsFocused && !AllSelected && (_now - _focusedAt) / BlinkTime % 2 == 0)
        {
            dc.Fill(new Rect(inner.X + caretX - _scroll, y, 1, h), Theme.Accent);
        }

        dc.PopClip();
    }

    protected internal override void OnTick(long now) => _now = now;

    protected internal override void OnFocusChanged(bool focused)
    {
        _focusedAt = _now;
        _all = focused;
        _focusedByClick = false;
        _caret = _text.Length;
    }

    protected internal override void OnMouseDown(int x, int y, bool right)
    {
        // The click that entered the box (focus comes just before it, in the
        // same moment) keeps everything selected; a later click places the caret.
        if (_all && !_focusedByClick && _focusedAt == _now)
        {
            _focusedByClick = true;
            return;
        }

        _all = false;
        var font = Theme.Fonts(FontIndex);
        var local = x - (Bounds.X + Theme.FieldTextInset) + _scroll;
        _caret = 0;
        while (_caret < _text.Length && Theme.Measure(FontIndex, Shown[..(_caret + 1)]) <= local)
        {
            _caret++;
        }

        _focusedAt = _now;
    }

    /// <summary>A double click selects all the text.</summary>
    protected internal override void OnDoubleClick(int x, int y)
    {
        _all = true;
        _caret = _text.Length;
        _focusedAt = _now;
    }

    protected internal override void OnText(char c)
    {
        if (char.IsControl(c))
        {
            return;
        }

        if (AllSelected)
        {
            Text = "";
            _caret = 0;
        }

        // Typing always ends the selection, even of an empty box.
        _all = false;
        if (_text.Length >= MaxLength)
        {
            return;
        }

        var at = _caret;
        Text = _text.Insert(at, c.ToString());
        _caret = at + 1;
        _focusedAt = _now;
    }

    protected internal override bool OnKey(FormKey key, bool shift)
    {
        _focusedAt = _now;
        if (AllSelected && key is FormKey.Backspace or FormKey.Delete)
        {
            _all = false;
            Text = "";
            _caret = 0;
            return true;
        }

        if (key is FormKey.Left or FormKey.Right or FormKey.Home or FormKey.End)
        {
            _all = false;
        }

        switch (key)
        {
            case FormKey.Left:
                _caret = Math.Max(0, _caret - 1);
                return true;
            case FormKey.Right:
                _caret = Math.Min(_text.Length, _caret + 1);
                return true;
            case FormKey.Home:
                _caret = 0;
                return true;
            case FormKey.End:
                _caret = _text.Length;
                return true;
            case FormKey.Backspace when _caret > 0:
                var at = _caret - 1;
                Text = _text.Remove(at, 1);
                _caret = at;
                return true;
            case FormKey.Delete when _caret < _text.Length:
                Text = _text.Remove(_caret, 1);
                return true;
            case FormKey.Return:
                Submitted?.Invoke(this, EventArgs.Empty);
                return true;
            case FormKey.Backspace or FormKey.Delete or FormKey.Space:
                return true;
            default:
                return false;
        }
    }
}
