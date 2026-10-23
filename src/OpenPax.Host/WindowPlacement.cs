using OpenPax.Sdl;
using OpenPax.Ui;

namespace OpenPax.Host;

/// <summary>
/// The window opens where the player left it: on the same display, at the
/// same place and size, maximized if it was (fullscreen is its own setting,
/// and covers the display the window is on). Each move, resize and change of
/// state is kept in the settings as it happens.
/// </summary>
internal static class WindowPlacement
{
    // How much of a saved window must be on a display now for it to open
    // there: enough to grab its title bar. Displays may have been unplugged.
    private const int Grab = 48;

    /// <summary>Makes the window, hidden, at the saved place if it is still on a display, else centered on the main one at <paramref name="size"/>; then shows it.</summary>
    public static nint Open(string title, (int Width, int Height) size, IReadOnlyDictionary<string, int> saved, bool remember)
    {
        var window = Sdl.Sdl.CreateWindow(title, size.Width, size.Height, WindowFlags.Resizable | WindowFlags.Hidden);
        if (window == 0)
        {
            return 0;
        }

        if (remember && Saved(saved) is { } place && OnADisplay(place, Sdl.Sdl.DisplayAreas()))
        {
            Sdl.Sdl.SetWindowSize(window, place.Width, place.Height);
            Sdl.Sdl.SetWindowPosition(window, place.X, place.Y);
            if (saved.GetValueOrDefault(Preferences.WindowMaximized) != 0)
            {
                Sdl.Sdl.MaximizeWindow(window);
            }
        }

        Sdl.Sdl.ShowWindow(window);
        return window;
    }

    /// <summary>A window event: the place and state go into the settings, to be written with them.</summary>
    public static void Follow(nint window, EventType type, Preferences preferences)
    {
        switch (type)
        {
            case EventType.WindowMoved or EventType.WindowResized:
                // Only a plain window's place is kept: a maximized or fullscreen
                // one would come back as that size with nowhere to restore to.
                var flags = Sdl.Sdl.GetWindowFlags(window);
                if ((flags & ((ulong)WindowFlags.Fullscreen | Sdl.Sdl.WindowMaximizedFlag | Sdl.Sdl.WindowMinimizedFlag)) == 0
                    && Sdl.Sdl.WindowPosition(window) is { } at && Sdl.Sdl.WindowSize(window) is { } size && size.Width > 0)
                {
                    preferences.Set(Preferences.WindowX, at.X);
                    preferences.Set(Preferences.WindowY, at.Y);
                    preferences.Set(Preferences.WindowWidth, size.Width);
                    preferences.Set(Preferences.WindowHeight, size.Height);
                }

                break;
            case EventType.WindowMaximized:
                preferences.Set(Preferences.WindowMaximized, 1);
                break;
            case EventType.WindowRestored when (Sdl.Sdl.GetWindowFlags(window) & Sdl.Sdl.WindowMaximizedFlag) == 0:
                preferences.Set(Preferences.WindowMaximized, 0);
                break;
            case EventType.WindowEnterFullscreen:
                // The system's own fullscreen (a title bar button, a window
                // manager key) counts as the setting, so it comes back too.
                preferences.Set(Preferences.Fullscreen, 1);
                break;
            case EventType.WindowLeaveFullscreen:
                preferences.Set(Preferences.Fullscreen, 0);
                break;
        }
    }

    internal static (int X, int Y, int Width, int Height)? Saved(IReadOnlyDictionary<string, int> saved) =>
        saved.GetValueOrDefault(Preferences.WindowWidth) is var w and > 0 && saved.GetValueOrDefault(Preferences.WindowHeight) is var h and > 0
            ? (saved.GetValueOrDefault(Preferences.WindowX), saved.GetValueOrDefault(Preferences.WindowY), w, h)
            : null;

    /// <summary>Whether enough of the window's top edge lies on one of the displays to take hold of it.</summary>
    internal static bool OnADisplay((int X, int Y, int Width, int Height) place, IEnumerable<(int X, int Y, int Width, int Height)> displays) =>
        displays.Any(d =>
        {
            var left = Math.Max(place.X, d.X);
            var right = Math.Min(place.X + place.Width, d.X + d.Width);
            return right - left >= Grab && place.Y >= d.Y - Grab && place.Y < d.Y + d.Height - Grab;
        });
}
