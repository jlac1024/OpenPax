using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

// SDL_events.h
public static unsafe partial class Sdl
{
    /// <summary>SDL_PollEvent. Returns false when the queue is empty.</summary>
    public static bool PollEvent(out Event ev)
    {
        // Event is a blittable 128 byte struct matching SDL_Event, and the
        // pointer only lives for the duration of the call.
        fixed (Event* p = &ev)
        {
            return SDL_PollEvent(p) != 0;
        }
    }

    [LibraryImport(LibraryName)]
    private static partial byte SDL_PollEvent(Event* ev);
}
