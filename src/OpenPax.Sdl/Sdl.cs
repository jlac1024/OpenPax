using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

/// <summary>
/// Hand-written SDL3 bindings. Only the functions the port actually calls are
/// declared; add more as they are needed, grouped by SDL header in partial files.
/// </summary>
public static unsafe partial class Sdl
{
    // Resolved by the host's platform folder to the SDL3 library shipped next
    // to the executable.
    public const string LibraryName = "SDL3";

    /// <summary>SDL_GetError: the message for the last failed call on this thread.</summary>
    public static string GetError()
    {
        // SDL owns the returned buffer and keeps it valid until the next SDL
        // call on this thread, so reading it straight away is safe.
        return Marshal.PtrToStringUTF8((nint)SDL_GetError()) ?? string.Empty;
    }

    [LibraryImport(LibraryName)]
    private static partial byte* SDL_GetError();
}
