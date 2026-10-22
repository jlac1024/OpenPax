namespace OpenPax.Sdl;

/// <summary>SDL_WindowFlags (64 bit in SDL3).</summary>
[Flags]
public enum WindowFlags : ulong
{
    None = 0,
    Fullscreen = 0x0000000000000001,
    OpenGL = 0x0000000000000002,
    Hidden = 0x0000000000000008,
    Borderless = 0x0000000000000010,
    Resizable = 0x0000000000000020,
    HighPixelDensity = 0x0000000000002000,
    Vulkan = 0x0000000010000000,
    Metal = 0x0000000020000000,
}
