using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

// SDL_init.h
public static partial class Sdl
{
    [LibraryImport(LibraryName, EntryPoint = "SDL_Init")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool Init(InitFlags flags);

    [LibraryImport(LibraryName, EntryPoint = "SDL_Quit")]
    public static partial void Quit();
}
