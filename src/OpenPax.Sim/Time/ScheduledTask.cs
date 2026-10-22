namespace OpenPax.Sim.Time;

/// <summary>A task on the scheduler: it runs when its countdown reaches zero, then waits a full period again.</summary>
public sealed class ScheduledTask(TaskKind kind, int period)
{
    public TaskKind Kind { get; } = kind;

    public int Period { get; } = period;

    public int Countdown { get; internal set; } = period;
}
