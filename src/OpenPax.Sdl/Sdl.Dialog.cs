using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

// SDL_dialog.h: the system's own open-file and open-folder dialogs.
public static unsafe partial class Sdl
{
    // Results of dialogs as SDL calls back, perhaps on another thread: request, chosen path (null for none), whether it failed.
    private static readonly ConcurrentQueue<(int Request, string? Path, bool Failed)> _dialogResults = new();

    // What each open request has allocated for SDL to read until its answer: freed then.
    private static readonly ConcurrentDictionary<int, nint[]> _dialogMemory = new();
    private static int _nextDialog;

    /// <summary>Opens the system's folder picker. The answer comes later from <see cref="TakeDialogResult"/>, with this request's number.</summary>
    public static int ShowOpenFolderDialog(nint window, string? start)
    {
        var request = Interlocked.Increment(ref _nextDialog);
        var location = start is null ? 0 : Marshal.StringToCoTaskMemUTF8(start);
        _dialogMemory[request] = [location];
        SDL_ShowOpenFolderDialog(&DialogDone, request, window, (byte*)location, false);
        return request;
    }

    /// <summary>Opens the system's file picker showing files of one kind ("Installers", "exe"); the answer comes later as for a folder.</summary>
    public static int ShowOpenFileDialog(nint window, string filterName, string filterPattern, string? start)
    {
        var request = Interlocked.Increment(ref _nextDialog);
        var name = Marshal.StringToCoTaskMemUTF8(filterName);
        var pattern = Marshal.StringToCoTaskMemUTF8(filterPattern);
        var location = start is null ? 0 : Marshal.StringToCoTaskMemUTF8(start);

        // SDL_DialogFileFilter: two string pointers, kept alive until the answer like the strings.
        var filter = Marshal.AllocCoTaskMem(2 * sizeof(nint));
        ((nint*)filter)[0] = name;
        ((nint*)filter)[1] = pattern;
        _dialogMemory[request] = [name, pattern, location, filter];
        SDL_ShowOpenFileDialog(&DialogDone, request, window, (void*)filter, 1, (byte*)location, false);
        return request;
    }

    /// <summary>A dialog's answer once it has come: its request, the path chosen (null when cancelled or it failed), and whether it failed.</summary>
    public static bool TakeDialogResult(out int request, out string? path, out bool failed)
    {
        if (_dialogResults.TryDequeue(out var result))
        {
            (request, path, failed) = result;
            return true;
        }

        (request, path, failed) = (0, null, false);
        return false;
    }

    // Sound because SDL gives back the request number it was given as userdata, and
    // a list of UTF-8 paths it owns and frees after this returns, so the first
    // is read now; the request's own allocations are freed only after SDL is done.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void DialogDone(nint userdata, byte** files, int filter)
    {
        var request = (int)userdata;
        var path = files is null || files[0] is null ? null : Marshal.PtrToStringUTF8((nint)files[0]);
        _dialogResults.Enqueue((request, path, files is null));
        if (_dialogMemory.TryRemove(request, out var memory))
        {
            foreach (var p in memory.Where(p => p != 0))
            {
                Marshal.FreeCoTaskMem(p);
            }
        }
    }

    [LibraryImport(LibraryName)]
    private static partial void SDL_ShowOpenFolderDialog(delegate* unmanaged[Cdecl]<nint, byte**, int, void> callback, nint userdata, nint window, byte* defaultLocation, [MarshalAs(UnmanagedType.U1)] bool allowMany);

    [LibraryImport(LibraryName)]
    private static partial void SDL_ShowOpenFileDialog(delegate* unmanaged[Cdecl]<nint, byte**, int, void> callback, nint userdata, nint window, void* filters, int filterCount, byte* defaultLocation, [MarshalAs(UnmanagedType.U1)] bool allowMany);
}
