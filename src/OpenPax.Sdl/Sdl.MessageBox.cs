using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

// SDL_messagebox.h
public static partial class Sdl
{
    private const uint MessageBoxError = 0x10;

    /// <summary>
    /// SDL_ShowSimpleMessageBox as an error: works before SDL_Init and with no
    /// window (pass zero), so it can report a crash. False if it could not show.
    /// </summary>
    public static bool ShowError(string title, string message, nint window = 0) =>
        SDL_ShowSimpleMessageBox(MessageBoxError, title, message, window);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SDL_ShowSimpleMessageBox(uint flags, string title, string message, nint window);
}
