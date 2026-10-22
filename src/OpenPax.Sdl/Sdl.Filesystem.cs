using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

public static unsafe partial class Sdl
{
    /// <summary>
    /// SDL_GetPrefPath: the per-user folder for saves and config, made if
    /// missing, ending in a separator. Null on failure; see <see cref="GetError"/>.
    /// </summary>
    public static string? GetPrefPath(string organization, string application)
    {
        var path = SDL_GetPrefPath(organization, application);
        if (path is null)
        {
            return null;
        }

        // The buffer is ours until SDL_free; it is copied before being freed.
        var text = Marshal.PtrToStringUTF8((nint)path);
        SDL_free(path);
        return text;
    }

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    private static partial byte* SDL_GetPrefPath(string org, string app);

    [LibraryImport(LibraryName)]
    private static partial void SDL_free(void* memory);
}
