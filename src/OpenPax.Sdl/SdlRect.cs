using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

/// <summary>SDL_Rect: a position and a size, four ints, as SDL lays it out.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SdlRect
{
    public int X;
    public int Y;
    public int W;
    public int H;
}
