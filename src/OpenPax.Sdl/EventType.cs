namespace OpenPax.Sdl;

/// <summary>SDL_EventType. Values from SDL_events.h.</summary>
public enum EventType : uint
{
    First = 0,
    Quit = 0x100,
    WindowMoved = 0x205,
    WindowResized = 0x206,
    WindowMinimized = 0x209,
    WindowMaximized = 0x20A,
    WindowRestored = 0x20B,
    WindowMouseEnter = 0x20C,
    WindowMouseLeave = 0x20D,
    WindowFocusGained = 0x20E,
    WindowFocusLost = 0x20F,
    WindowCloseRequested = 0x210,
    WindowEnterFullscreen = 0x217,
    WindowLeaveFullscreen = 0x218,
    KeyDown = 0x300,
    KeyUp = 0x301,
    TextInput = 0x303,
    MouseMotion = 0x400,
    MouseButtonDown = 0x401,
    MouseButtonUp = 0x402,
    MouseWheel = 0x403,
}
