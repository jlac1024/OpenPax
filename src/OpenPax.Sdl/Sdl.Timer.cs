using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

// SDL_timer.h
public static partial class Sdl
{
    [LibraryImport(LibraryName, EntryPoint = "SDL_Delay")]
    public static partial void Delay(uint milliseconds);

    /// <summary>SDL_GetTicks: milliseconds since SDL_Init. Platform timing only, never simulation input.</summary>
    [LibraryImport(LibraryName, EntryPoint = "SDL_GetTicks")]
    public static partial ulong GetTicks();

    /// <summary>SDL_GetTicksNS: nanoseconds since SDL_Init, for frame pacing.</summary>
    [LibraryImport(LibraryName, EntryPoint = "SDL_GetTicksNS")]
    public static partial ulong GetTicksNs();

    /// <summary>SDL_DelayPrecise: sleeps, then spins the last moment, so a frame ends close to its deadline.</summary>
    [LibraryImport(LibraryName, EntryPoint = "SDL_DelayPrecise")]
    public static partial void DelayPrecise(ulong nanoseconds);
}
