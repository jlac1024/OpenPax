using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenPax.Media.Video;

namespace OpenPax.Media.Tests;

/// <summary>
/// Points the Media bindings at build/native/ffmpeg-libs for tests, as the
/// host does with the copies in deploy/. Linux file names only; these tests
/// run on the development machine.
/// </summary>
internal static class FfmpegForTests
{
    private static readonly Dictionary<string, nint> _loaded = [];

    public static string? LibraryFolder { get; private set; }

    [ModuleInitializer]
    internal static void Register()
    {
        LibraryFolder = Find();
        NativeLibrary.SetDllImportResolver(typeof(FfmpegLibraries).Assembly, (name, _, _) =>
        {
            if (LibraryFolder is null || !FfmpegLibraries.LoadOrder.Contains(name))
            {
                return 0;
            }

            lock (_loaded)
            {
                foreach (var lib in FfmpegLibraries.LoadOrder)
                {
                    if (!_loaded.ContainsKey(lib))
                    {
                        _loaded[lib] = NativeLibrary.Load(Path.Combine(LibraryFolder, $"lib{lib}.so.{FfmpegLibraries.Major(lib)}"));
                    }

                    if (lib == name)
                    {
                        break;
                    }
                }

                return _loaded[name];
            }
        });
    }

    private static string? Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var lib = Path.Combine(dir.FullName, "build", "native", "ffmpeg-libs", "lib");
            if (Directory.Exists(lib))
            {
                return lib;
            }
        }

        return null;
    }
}
