using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

// SDL_video.h: displays and fullscreen.
public static unsafe partial class Sdl
{
    /// <summary>The primary display's desktop size in points, or null when SDL cannot tell.</summary>
    public static (int Width, int Height)? DesktopSize()
    {
        var display = SDL_GetPrimaryDisplay();
        if (display == 0)
        {
            return null;
        }

        // SDL owns the mode it returns and keeps it alive; only two ints are read from it.
        var mode = (int*)SDL_GetDesktopDisplayMode(display);
        return mode is null ? null : (mode[2], mode[3]);
    }

    /// <summary>The primary display's refresh rate in hertz, or null when SDL cannot tell.</summary>
    public static float? RefreshRate()
    {
        var display = SDL_GetPrimaryDisplay();
        if (display == 0)
        {
            return null;
        }

        // SDL_DisplayMode: id, format, w, h, pixel_density (float), refresh_rate (float): the sixth 4-byte field. SDL owns it and keeps it alive.
        var mode = (byte*)SDL_GetDesktopDisplayMode(display);
        var rate = mode is null ? 0f : *(float*)(mode + 20);
        return rate > 1 ? rate : null;
    }

    /// <summary>SDL_SetWindowFullscreen: on, it covers the display at the desktop's own resolution (no mode change).</summary>
    [LibraryImport(LibraryName, EntryPoint = "SDL_SetWindowFullscreen")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetWindowFullscreen(nint window, [MarshalAs(UnmanagedType.U1)] bool fullscreen);

    [LibraryImport(LibraryName)]
    private static partial uint SDL_GetPrimaryDisplay();

    [LibraryImport(LibraryName)]
    private static partial void* SDL_GetDesktopDisplayMode(uint display);
}
