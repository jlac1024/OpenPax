namespace OpenPax.Audio;

/// <summary>
/// The sound device (SDL3 audio stream, pushed from the main loop). Opening
/// fails quietly on a machine with no sound: the game then runs silent.
/// </summary>
public sealed class AudioOutput : IDisposable
{
    // About a tenth of a second queued ahead: enough to ride out a slow frame.
    private const int AheadFrames = Mixer.Rate / 10;
    private const int ChunkFrames = 1024;

    private readonly nint _stream;
    private readonly short[] _chunk = new short[ChunkFrames * 2];

    private AudioOutput(nint stream)
    {
        _stream = stream;
    }

    /// <summary>The default playback device, or null when there is none.</summary>
    public static AudioOutput? Open()
    {
        if (!Sdl.Sdl.InitSubSystem(Sdl.InitFlags.Audio))
        {
            return null;
        }

        var stream = Sdl.Sdl.OpenAudioDeviceStream(Sdl.Sdl.DefaultPlaybackDevice, new Sdl.Sdl.AudioSpec { Format = Sdl.Sdl.AudioS16, Channels = 2, Freq = Mixer.Rate });
        if (stream == 0)
        {
            return null;
        }

        Sdl.Sdl.ResumeAudioStreamDevice(stream);
        return new AudioOutput(stream);
    }

    /// <summary>Tops the device's queue up from the mixer.</summary>
    public void Pump(Mixer mixer)
    {
        var bytesPerFrame = 2 * sizeof(short);
        while (Sdl.Sdl.GetAudioStreamQueued(_stream) is var queued and >= 0 && queued / bytesPerFrame < AheadFrames)
        {
            mixer.Mix(_chunk);
            if (!Sdl.Sdl.PutAudioStreamData(_stream, _chunk))
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        Sdl.Sdl.DestroyAudioStream(_stream);
    }
}
