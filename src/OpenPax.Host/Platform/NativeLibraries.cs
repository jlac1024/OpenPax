using System.Reflection;
using System.Runtime.InteropServices;
using OpenPax.Media.Video;

namespace OpenPax.Host.Platform;

/// <summary>
/// Points our bindings at the native libraries shipped next to the
/// executable (SDL3 and the decode-only FFmpeg libraries), so the game never
/// picks up different versions installed on the system.
/// </summary>
internal static class NativeLibraries
{
    // FFmpeg libraries name each other by soname and carry no search path,
    // so they are loaded in dependency order: each one's dependencies are
    // then already in the process.
    private static readonly Dictionary<string, nint> _ffmpeg = [];
    private static readonly Lock _lock = new();

    public static void Register()
    {
        NativeLibrary.SetDllImportResolver(typeof(Sdl.Sdl).Assembly, ResolveSdl);
        NativeLibrary.SetDllImportResolver(typeof(FfmpegLibraries).Assembly, ResolveFfmpeg);
    }

    private static nint ResolveSdl(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        return name == Sdl.Sdl.LibraryName ? Load(SdlFileName()) : 0;
    }

    private static nint ResolveFfmpeg(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!FfmpegLibraries.LoadOrder.Contains(name))
        {
            return 0;
        }

        lock (_lock)
        {
            foreach (var lib in FfmpegLibraries.LoadOrder)
            {
                if (!_ffmpeg.ContainsKey(lib))
                {
                    _ffmpeg[lib] = Load(FfmpegFileName(lib));
                }

                if (lib == name)
                {
                    break;
                }
            }

            return _ffmpeg[name];
        }
    }

    private static nint Load(string fileName)
    {
        return NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, fileName));
    }

    private static string SdlFileName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "SDL3.dll";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "libSDL3.0.dylib";
        }

        // The SONAME. A plain file, not a symlink, so deploy/ survives being copied.
        return "libSDL3.so.0";
    }

    private static string FfmpegFileName(string name)
    {
        var major = FfmpegLibraries.Major(name);

        if (OperatingSystem.IsWindows())
        {
            return $"{name}-{major}.dll";
        }

        if (OperatingSystem.IsMacOS())
        {
            return $"lib{name}.{major}.dylib";
        }

        return $"lib{name}.so.{major}";
    }
}
