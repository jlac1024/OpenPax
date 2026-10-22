using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

/// <summary>
/// SDL_Event. SDL3 pads the union to 128 bytes; only the common header is
/// exposed for now. Typed views of the union get added as input handling needs them.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct Event
{
    [FieldOffset(0)]
    public EventType Type;

    [FieldOffset(8)]
    public ulong Timestamp;

    /// <summary>Mouse button events: which button (1 left).</summary>
    [FieldOffset(24)]
    public byte MouseButton;

    /// <summary>Mouse button events: 1 for a single click, 2 for a double click.</summary>
    [FieldOffset(26)]
    public byte MouseClicks;

    /// <summary>Mouse motion and button events: position in window pixels.</summary>
    [FieldOffset(28)]
    public float MouseX;

    [FieldOffset(32)]
    public float MouseY;

    /// <summary>Mouse wheel events: the amount turned, positive away from the user.</summary>
    [FieldOffset(28)]
    public float WheelY;

    /// <summary>Key events: the SDL_Keycode.</summary>
    [FieldOffset(28)]
    public uint Key;

    /// <summary>Key events: the SDL_Keymod held.</summary>
    [FieldOffset(32)]
    public ushort KeyMod;

    /// <summary>Text input events: the UTF-8 text, owned by SDL until the next poll.</summary>
    [FieldOffset(24)]
    public nint Text;
}
