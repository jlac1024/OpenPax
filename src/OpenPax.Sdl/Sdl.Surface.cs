using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

// SDL_surface.h and the window icon.
public static unsafe partial class Sdl
{
    /// <summary>The window's icon (the taskbar's and the title bar's), from pixels four bytes each, R, G, B, A, row by row.</summary>
    public static bool SetWindowIcon(nint window, int width, int height, byte[] rgba)
    {
        if (rgba.Length < width * height * 4)
        {
            return false;
        }

        // Sound because the surface only borrows the pinned array, SDL copies the
        // icon before SDL_SetWindowIcon returns; the surface goes before the pin ends.
        fixed (byte* pixels = rgba)
        {
            var surface = SDL_CreateSurfaceFrom(width, height, PixelFormatRgba32, pixels, width * 4);
            if (surface == 0)
            {
                return false;
            }

            var done = SDL_SetWindowIcon(window, surface);
            SDL_DestroySurface(surface);
            return done;
        }
    }

    [LibraryImport(LibraryName)]
    private static partial nint SDL_CreateSurfaceFrom(int width, int height, uint format, void* pixels, int pitch);

    [LibraryImport(LibraryName)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SDL_SetWindowIcon(nint window, nint surface);

    [LibraryImport(LibraryName)]
    private static partial void SDL_DestroySurface(nint surface);
}
