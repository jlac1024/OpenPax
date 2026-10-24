namespace OpenPax.Ui.Screens;

/// <summary>
/// One full screen: its window and what it does with what the widgets
/// report. Screens are built afresh on every switch, as the original
/// rebuilds its windows, so none keeps state of its own between visits.
/// </summary>
public abstract class Screen
{
    protected Screen(UiApp app, ViewWindow window)
    {
        App = app;
        Window = window;
    }

    protected UiApp App { get; }

    public ViewWindow Window { get; }

    /// <summary>One message from the window's widgets.</summary>
    public abstract void Handle(UiMessage message, long now);

    /// <summary>
    /// Where the screen widens in a wide window (docs/ui/widescreen.md): a
    /// layout's spans of its 640 wide art that take the extra width. Null
    /// keeps the original's 4:3 picture.
    /// </summary>
    public virtual WideLayout? WideAt => null;

    /// <summary>
    /// The port's own: what the thing under a point (the screen's own
    /// coordinates) is for, shown as a tooltip when the pointer rests on it;
    /// a heading line, then the explanation. Null where there is nothing to say.
    /// </summary>
    public virtual string? TooltipAt(int x, int y) => null;

    /// <summary>Lays the screen out larger by some amount across and down (0 for its own size).</summary>
    public void Widen(int extra, int extraHeight)
    {
        if (WideAt is { } at)
        {
            Window.Widen(extra, extraHeight, at);
            Widened(Window.Extra, Window.ExtraHeight);
        }
    }

    /// <summary>After the layout has grown: a screen moves what the plain rule does not suit.</summary>
    protected virtual void Widened(int extra, int extraHeight)
    {
    }

    /// <summary>Once a frame after the widgets have had the time.</summary>
    public virtual void Tick(long now)
    {
    }

    /// <summary>The screen is being left (the window's destructor in the original).</summary>
    public virtual void Closed()
    {
    }
}
