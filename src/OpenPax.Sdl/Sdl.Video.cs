using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

// SDL_video.h
public static partial class Sdl
{
    /// <summary>SDL_CreateWindow. Returns zero on failure; see <see cref="GetError"/>.</summary>
    [LibraryImport(LibraryName, EntryPoint = "SDL_CreateWindow", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint CreateWindow(string title, int width, int height, WindowFlags flags);

    [LibraryImport(LibraryName, EntryPoint = "SDL_DestroyWindow")]
    public static partial void DestroyWindow(nint window);
}
