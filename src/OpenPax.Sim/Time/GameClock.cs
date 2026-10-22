namespace OpenPax.Sim.Time;

/// <summary>
/// Paces ticks by real time, as the original's clock does. It never reads
/// the time itself: the host passes the current time in milliseconds, so
/// the simulation stays free of the wall clock.
/// </summary>
public sealed class GameClock
{
    public const int DefaultSpeed = 3;
    public const int CombatInterval = 40;

    private long _lastStrategic;
    private long _lastCombat;

    public GameClock(long now)
    {
        _lastStrategic = now;
        _lastCombat = now;
        SetSpeed(DefaultSpeed);
    }

    /// <summary>Milliseconds per strategic tick.</summary>
    public int Interval { get; private set; }

    public int Speed { get; private set; }

    public bool Paused { get; set; }

    /// <summary>Combat runs on its own 40 ms timer instead of the strategic one.</summary>
    public bool InCombat { get; set; }

    /// <summary>
    /// The port's own toggle (docs/sim/time.md): speed 0 holds the game, as
    /// a pause, instead of the original's slowest speed (600 ms a step).
    /// </summary>
    public bool ZeroPauses { get; set; }

    /// <summary>The game is held: paused, or at speed 0 with <see cref="ZeroPauses"/>.</summary>
    public bool Held => Paused || (ZeroPauses && Speed == 0);

    /// <summary>Run one step on the next poll even if its interval has not passed.</summary>
    public bool StepNow { get; set; }

    /// <summary>Speeds 0 (slowest) to 6; any other value is ignored, as in the original.</summary>
    public void SetSpeed(int speed)
    {
        int? interval = speed switch
        {
            0 => 600,
            1 or 2 => 450,
            3 => 150,
            4 => 75,
            5 => 38,
            6 => 13,
            _ => null,
        };
        if (interval is { } ms)
        {
            Interval = ms;
            Speed = speed;
        }
    }

    /// <summary>
    /// Whether one step is due now. At most one step per poll: a slow frame
    /// loses time instead of catching up, as in the original. A forced step
    /// does not restart the timer.
    /// </summary>
    public bool Poll(long now)
    {
        if (Paused)
        {
            return false;
        }

        // Held at speed 0: combat stops too, as with a pause; a forced step still runs.
        if (ZeroPauses && Speed == 0)
        {
            return StepNow;
        }

        ref var last = ref InCombat ? ref _lastCombat : ref _lastStrategic;
        var interval = InCombat ? CombatInterval : Interval;
        if (now - last >= interval)
        {
            last = now;
            return true;
        }

        return StepNow;
    }
}
