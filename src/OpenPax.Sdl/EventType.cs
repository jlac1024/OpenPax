namespace OpenPax.Sdl;

/// <summary>SDL_EventType. Values from SDL_events.h.</summary>
public enum EventType : uint
{
    First = 0,
    Quit = 0x100,
    WindowMouseEnter = 0x20C,
    WindowMouseLeave = 0x20D,
    WindowFocusGained = 0x20E,
    WindowFocusLost = 0x20F,
    WindowCloseRequested = 0x210,
    KeyDown = 0x300,
    KeyUp = 0x301,
    TextInput = 0x303,
    MouseMotion = 0x400,
    MouseButtonDown = 0x401,
    MouseButtonUp = 0x402,
    MouseWheel = 0x403,
}
