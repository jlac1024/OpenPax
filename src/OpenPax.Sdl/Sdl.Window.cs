using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

// SDL_video.h: where a window is, how big, and the displays it may go on.
public static unsafe partial class Sdl
{
    public const ulong WindowMinimizedFlag = 0x40;
    public const ulong WindowMaximizedFlag = 0x80;

    /// <summary>The window's top-left corner in desktop coordinates, or null when SDL cannot tell.</summary>
    public static (int X, int Y)? WindowPosition(nint window)
    {
        // Sound: SDL writes two ints through pointers to these locals, which live for the call.
        int x, y;
        return SDL_GetWindowPosition(window, &x, &y) ? (x, y) : null;
    }

    /// <summary>The window's size in points (not counting its frame), or null when SDL cannot tell.</summary>
    public static (int Width, int Height)? WindowSize(nint window)
    {
        // Sound: as for the position, two ints written into locals.
        int w, h;
        return SDL_GetWindowSize(window, &w, &h) ? (w, h) : null;
    }

    /// <summary>
    /// Each display's usable area (the desktop less task bars and docks) in
    /// desktop coordinates, so a saved window place can be checked against
    /// the displays there are now.
    /// </summary>
    public static IReadOnlyList<(int X, int Y, int Width, int Height)> DisplayAreas()
    {
        int count;
        var ids = SDL_GetDisplays(&count);
        if (ids is null)
        {
            return [];
        }

        // Sound: SDL gives an array of count ids it allocated, read within
        // its length, then freed; each rect is a local SDL fills in.
        var areas = new List<(int, int, int, int)>();
        for (var i = 0; i < count; i++)
        {
            SdlRect r;
            if (SDL_GetDisplayUsableBounds(ids[i], &r))
            {
                areas.Add((r.X, r.Y, r.W, r.H));
            }
        }

        SDL_free(ids);
        return areas;
    }

    [LibraryImport(LibraryName, EntryPoint = "SDL_SetWindowPosition")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetWindowPosition(nint window, int x, int y);

    [LibraryImport(LibraryName, EntryPoint = "SDL_SetWindowSize")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetWindowSize(nint window, int width, int height);

    /// <summary>SDL_GetWindowFlags: fullscreen, minimized, maximized and the rest, as SDL_WindowFlags bits.</summary>
    [LibraryImport(LibraryName, EntryPoint = "SDL_GetWindowFlags")]
    public static partial ulong GetWindowFlags(nint window);

    [LibraryImport(LibraryName, EntryPoint = "SDL_MaximizeWindow")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool MaximizeWindow(nint window);

    [LibraryImport(LibraryName, EntryPoint = "SDL_ShowWindow")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool ShowWindow(nint window);

    [LibraryImport(LibraryName)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SDL_GetWindowPosition(nint window, int* x, int* y);

    [LibraryImport(LibraryName)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SDL_GetWindowSize(nint window, int* w, int* h);

    [LibraryImport(LibraryName)]
    private static partial uint* SDL_GetDisplays(int* count);

    [LibraryImport(LibraryName)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SDL_GetDisplayUsableBounds(uint display, SdlRect* rect);
}
