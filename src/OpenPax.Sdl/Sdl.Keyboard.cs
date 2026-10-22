using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

// SDL_keyboard.h
public static partial class Sdl
{
    public const uint KeyReturn = 0x0D;
    public const uint KeyEscape = 0x1B;
    public const uint KeyBackspace = 0x08;
    public const uint KeyTab = 0x09;
    public const uint KeyDelete = 0x7F;
    public const uint KeyHome = 0x4000004A;
    public const uint KeyEnd = 0x4000004D;
    public const uint KeyRight = 0x4000004F;
    public const uint KeyLeft = 0x40000050;
    public const uint KeyDown = 0x40000051;
    public const uint KeyUp = 0x40000052;
    public const uint KeyPadEnter = 0x40000058;
    public const uint KeyMinus = 0x2D;
    public const uint KeyGrave = 0x60;
    public const uint KeyEquals = 0x3D;
    public const uint KeyBackslash = 0x5C;
    public const uint KeyN = 0x6E;
    public const uint KeyX = 0x78;

    /// <summary>F1; F2 to F12 follow in order.</summary>
    public const uint KeyF1 = 0x4000003A;

    /// <summary>SDL_KMOD_CTRL: either control key.</summary>
    public const ushort ModCtrl = 0x00C0;

    /// <summary>SDL_KMOD_ALT: either alt key.</summary>
    public const ushort ModAlt = 0x0300;

    /// <summary>SDL_KMOD_SHIFT: either shift key.</summary>
    public const ushort ModShift = 0x0003;

    [LibraryImport(LibraryName, EntryPoint = "SDL_StartTextInput")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool StartTextInput(nint window);

    [LibraryImport(LibraryName, EntryPoint = "SDL_StopTextInput")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool StopTextInput(nint window);

    /// <summary>The text of a text input event, copied out before the next poll frees it.</summary>
    public static string TextOf(in Event ev)
    {
        return Marshal.PtrToStringUTF8(ev.Text) ?? "";
    }
}
