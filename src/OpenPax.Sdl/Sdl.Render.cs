using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

// SDL_render.h
public static unsafe partial class Sdl
{
    /// <summary>SDL_PIXELFORMAT_ABGR8888: bytes R, G, B, A in memory on little-endian machines.</summary>
    public const uint PixelFormatRgba32 = 0x16762004;

    public const int TextureAccessStreaming = 1;
    public const int LogicalPresentationLetterbox = 2;
    public const int ScaleModeNearest = 0;
    public const int ScaleModeLinear = 1;
    public const int LogicalPresentationDisabled = 0;
    public const uint BlendModeBlend = 1;

    /// <summary>SDL_CreateRenderer with SDL's default driver. Returns zero on failure.</summary>
    public static nint CreateRenderer(nint window)
    {
        return SDL_CreateRenderer(window, null);
    }

    [LibraryImport(LibraryName, EntryPoint = "SDL_DestroyRenderer")]
    public static partial void DestroyRenderer(nint renderer);

    [LibraryImport(LibraryName, EntryPoint = "SDL_SetRenderLogicalPresentation")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetRenderLogicalPresentation(nint renderer, int width, int height, int mode);

    [LibraryImport(LibraryName, EntryPoint = "SDL_CreateTexture")]
    public static partial nint CreateTexture(nint renderer, uint format, int access, int width, int height);

    [LibraryImport(LibraryName, EntryPoint = "SDL_DestroyTexture")]
    public static partial void DestroyTexture(nint texture);

    [LibraryImport(LibraryName, EntryPoint = "SDL_SetTextureScaleMode")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetTextureScaleMode(nint texture, int mode);

    /// <summary>SDL_UpdateTexture for the whole texture.</summary>
    public static bool UpdateTexture(nint texture, ReadOnlySpan<byte> pixels, int pitch)
    {
        // The span is pinned for the duration of the call, and SDL copies
        // the pixels before returning.
        fixed (byte* p = pixels)
        {
            return SDL_UpdateTexture(texture, null, p, pitch) != 0;
        }
    }

    [LibraryImport(LibraryName, EntryPoint = "SDL_SetRenderDrawColor")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetRenderDrawColor(nint renderer, byte r, byte g, byte b, byte a);

    [LibraryImport(LibraryName, EntryPoint = "SDL_RenderClear")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool RenderClear(nint renderer);

    /// <summary>SDL_RenderTexture of the whole texture onto the whole (logical) target.</summary>
    public static bool RenderTexture(nint renderer, nint texture)
    {
        return SDL_RenderTexture(renderer, texture, null, null) != 0;
    }

    /// <summary>SDL_RenderTexture of the whole texture onto a rectangle of the target.</summary>
    public static bool RenderTexture(nint renderer, nint texture, float x, float y, float width, float height)
    {
        // The rectangle is a local that lives through the call.
        var destination = stackalloc float[4] { x, y, width, height };
        return SDL_RenderTexture(renderer, texture, null, destination) != 0;
    }

    /// <summary>The target's size in pixels, which on a high density display is more than the window's size in points.</summary>
    public static (int Width, int Height) RenderOutputSize(nint renderer)
    {
        int w, h;
        // Both outputs are locals that live through the call.
        SDL_GetRenderOutputSize(renderer, &w, &h);
        return (w, h);
    }

    [LibraryImport(LibraryName, EntryPoint = "SDL_SetTextureBlendMode")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetTextureBlendMode(nint texture, uint mode);

    /// <summary>SDL_SetRenderVSync: 1 waits for the display's refresh on present, 0 does not.</summary>
    [LibraryImport(LibraryName, EntryPoint = "SDL_SetRenderVSync")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetRenderVSync(nint renderer, int vsync);

    [LibraryImport(LibraryName, EntryPoint = "SDL_RenderPresent")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool RenderPresent(nint renderer);

    /// <summary>Window pixel coordinates to the renderer's logical coordinates.</summary>
    public static (float X, float Y) RenderCoordinatesFromWindow(nint renderer, float windowX, float windowY)
    {
        float x, y;
        // Both outputs are locals that live through the call.
        SDL_RenderCoordinatesFromWindow(renderer, windowX, windowY, &x, &y);
        return (x, y);
    }

    [LibraryImport(LibraryName)]
    private static partial nint SDL_CreateRenderer(nint window, byte* name);

    [LibraryImport(LibraryName)]
    private static partial byte SDL_UpdateTexture(nint texture, void* rect, byte* pixels, int pitch);

    [LibraryImport(LibraryName)]
    private static partial byte SDL_RenderTexture(nint renderer, nint texture, void* source, void* destination);

    [LibraryImport(LibraryName)]
    private static partial byte SDL_GetRenderOutputSize(nint renderer, int* width, int* height);

    [LibraryImport(LibraryName)]
    private static partial byte SDL_RenderCoordinatesFromWindow(nint renderer, float windowX, float windowY, float* x, float* y);
}
