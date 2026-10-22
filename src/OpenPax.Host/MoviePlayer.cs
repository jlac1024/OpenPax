using OpenPax.Audio;
using OpenPax.Media.Video;
using OpenPax.Render;
using OpenPax.Sdl;

namespace OpenPax.Host;

/// <summary>
/// Plays a converted movie full screen with its sound through the mixer.
/// A click or key press skips it.
/// </summary>
internal static class MoviePlayer
{
    /// <summary>False when the window was closed during the movie.</summary>
    public static bool Play(Screen screen, string path, Mixer mixer, AudioOutput? audio)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine(string.Format(Strings.MovieMissing, path));
            return true;
        }

        using var movie = new MovieDecoder(path);
        var start = Sdl.Sdl.GetTicks();
        while (movie.Next() is { } item)
        {
            if (item is AudioChunk sound)
            {
                // With no device nothing would ever drain the queue.
                if (audio is not null)
                {
                    mixer.QueueMovie(sound.Samples, sound.SampleRate, sound.Channels);
                }

                continue;
            }

            if (item is not VideoFrame frame)
            {
                continue;
            }

            audio?.Pump(mixer);

            while (Sdl.Sdl.PollEvent(out var ev))
            {
                switch (ev.Type)
                {
                    case EventType.Quit or EventType.WindowCloseRequested:
                        mixer.ClearMovie();
                        return false;
                    case EventType.KeyDown or EventType.MouseButtonDown:
                        mixer.ClearMovie();
                        return true;
                }
            }

            var due = start + (ulong)(frame.Time * 1000);
            var now = Sdl.Sdl.GetTicks();
            if (due > now)
            {
                Sdl.Sdl.Delay((uint)(due - now));
                audio?.Pump(mixer);
            }

            screen.PresentRgba(frame.Rgba, frame.Width, frame.Height);
        }

        return true;
    }
}
