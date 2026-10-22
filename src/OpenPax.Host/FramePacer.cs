namespace OpenPax.Host;

/// <summary>
/// Paces the main loop at the display's refresh rate: each frame ends at its
/// own deadline on SDL's nanosecond clock, rather than after a fixed sleep on
/// top of the frame's work (which ran under the rate and unevenly). A frame
/// that ran late starts the next one from now instead of rushing to catch
/// up. With vsync on, presenting already waits, so the pacer only steps in
/// when presenting returned early (a minimized or hidden window).
/// </summary>
internal sealed class FramePacer(Func<ulong> nanoseconds, Action<ulong> sleep)
{
    private ulong _deadline;
    private ulong _last;

    /// <summary>Nanoseconds per frame.</summary>
    public ulong Period { get; set; } = 1_000_000_000 / 60;

    /// <summary>The last frame's length, in milliseconds, for the frame time readout.</summary>
    public double LastFrameMs { get; private set; }

    public void SetRate(float hertz) => Period = (ulong)(1_000_000_000 / Math.Clamp(hertz, 24, 500));

    /// <summary>Waits out what is left of this frame.</summary>
    /// <param name="vsynced">Presenting waits for the display: the pacer then only sleeps for a frame that came back far too soon.</param>
    public void EndFrame(bool vsynced = false)
    {
        var now = nanoseconds();
        if (vsynced && _last != 0 && now - _last >= Period * 3 / 4)
        {
            // The display set the pace; pacing again here could miss the next refresh.
            _deadline = now;
            LastFrameMs = (now - _last) / 1_000_000.0;
            _last = now;
            return;
        }

        _deadline = _deadline == 0 || now > _deadline + Period ? now + Period : _deadline + Period;
        if (_deadline > now)
        {
            sleep(_deadline - now);
        }

        var end = nanoseconds();
        LastFrameMs = _last == 0 ? 0 : (end - _last) / 1_000_000.0;
        _last = end;
    }
}
