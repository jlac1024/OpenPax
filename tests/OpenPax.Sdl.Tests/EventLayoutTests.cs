using System.Runtime.CompilerServices;
using OpenPax.Sdl;

namespace OpenPax.Sdl.Tests;

public class EventLayoutTests
{
    [Fact]
    public void EventMatchesSdlEventSize()
    {
        // SDL3 pads SDL_Event to 128 bytes; a smaller struct would let
        // SDL_PollEvent write past it.
        Assert.Equal(128, Unsafe.SizeOf<Event>());
    }
}
