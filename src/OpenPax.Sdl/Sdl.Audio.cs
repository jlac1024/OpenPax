using System.Runtime.InteropServices;

namespace OpenPax.Sdl;

// SDL_audio.h, SDL_init.h (subsystems)
public static unsafe partial class Sdl
{
    /// <summary>SDL_AUDIO_S16LE: signed 16-bit samples, little endian.</summary>
    public const uint AudioS16 = 0x8010;

    /// <summary>SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK.</summary>
    public const uint DefaultPlaybackDevice = 0xFFFFFFFF;

    /// <summary>SDL_AudioSpec.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AudioSpec
    {
        public uint Format;
        public int Channels;
        public int Freq;
    }

    [LibraryImport(LibraryName, EntryPoint = "SDL_InitSubSystem")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool InitSubSystem(InitFlags flags);

    /// <summary>SDL_OpenAudioDeviceStream with no callback: the caller pushes data. 0 on failure.</summary>
    public static nint OpenAudioDeviceStream(uint device, AudioSpec spec)
    {
        // The spec is copied by SDL during the call, so a pointer to this
        // local is valid for as long as SDL reads it.
        return SDL_OpenAudioDeviceStream(device, &spec, 0, 0);
    }

    [LibraryImport(LibraryName)]
    private static partial nint SDL_OpenAudioDeviceStream(uint device, AudioSpec* spec, nint callback, nint userdata);

    [LibraryImport(LibraryName, EntryPoint = "SDL_ResumeAudioStreamDevice")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool ResumeAudioStreamDevice(nint stream);

    /// <summary>SDL_PutAudioStreamData: queues samples for playback.</summary>
    public static bool PutAudioStreamData(nint stream, ReadOnlySpan<short> samples)
    {
        // The span is pinned for the call and SDL copies the data before
        // returning, so the pointer never outlives the pin.
        fixed (short* p = samples)
        {
            return SDL_PutAudioStreamData(stream, p, samples.Length * sizeof(short)) != 0;
        }
    }

    [LibraryImport(LibraryName)]
    private static partial byte SDL_PutAudioStreamData(nint stream, short* buf, int len);

    /// <summary>SDL_GetAudioStreamQueued: bytes still waiting to play, or -1.</summary>
    [LibraryImport(LibraryName, EntryPoint = "SDL_GetAudioStreamQueued")]
    public static partial int GetAudioStreamQueued(nint stream);

    [LibraryImport(LibraryName, EntryPoint = "SDL_DestroyAudioStream")]
    public static partial void DestroyAudioStream(nint stream);
}
