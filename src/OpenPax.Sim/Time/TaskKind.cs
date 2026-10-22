namespace OpenPax.Sim.Time;

/// <summary>The scheduler's periodic tasks (docs/sim/time.md), in the order they run each tick.</summary>
public enum TaskKind
{
    /// <summary>Every 4000 ticks, single player only.</summary>
    Autosave,

    /// <summary>Every 40 ticks, two passes over the players.</summary>
    Empire,

    /// <summary>Every 40 ticks.</summary>
    Espionage,

    /// <summary>Every 40 ticks.</summary>
    Research,

    /// <summary>Every 200 ticks, attitudes between each pair of players.</summary>
    Diplomacy,

    /// <summary>Every 2000 ticks.</summary>
    Advisors,

    /// <summary>Every 200 ticks; purpose not yet known.</summary>
    PlayerUpdate,

    /// <summary>Every 4 ticks.</summary>
    Calendar,

    /// <summary>Every 20, 10 or 5 ticks by difficulty: one computer player thinks.</summary>
    ComputerPlayer,

    /// <summary>Every tick: planets and fleets in every star system.</summary>
    StarSystems,
}
