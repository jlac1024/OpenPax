using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

// SDL_mouse.h
public static partial class Sdl
{
    /// <summary>SDL_HideCursor: the system pointer goes; the game draws its own.</summary>
    [LibraryImport(LibraryName, EntryPoint = "SDL_HideCursor")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool HideCursor();
}
