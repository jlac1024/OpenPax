using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

// SDL_init.h
public static partial class Sdl
{
    [LibraryImport(LibraryName, EntryPoint = "SDL_Init")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool Init(InitFlags flags);

    /// <summary>
    /// Names the app before SDL starts. The identifier is the Wayland app ID
    /// (and X11 class), which a desktop matches to the menu entry of the same
    /// name for the running window's icon.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "SDL_SetAppMetadata", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetAppMetadata(string name, string version, string identifier);

    [LibraryImport(LibraryName, EntryPoint = "SDL_Quit")]
    public static partial void Quit();
}
