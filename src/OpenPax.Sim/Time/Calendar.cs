namespace OpenPax.Sim.Time;

/// <summary>The in-game date: a year and a step within it (0 to 99, one step every 4 ticks).</summary>
public sealed class Calendar
{
    public const int FirstYear = 2100;
    public const int StepsPerYear = 100;

    public int Year { get; private set; } = FirstYear;

    /// <summary>The date as an original save holds it.</summary>
    internal void Restore(int year, int step)
    {
        Year = year;
        Step = step;
    }

    public int Step { get; private set; }

    internal void Advance()
    {
        Step++;
        if (Step == StepsPerYear)
        {
            Step = 0;
            Year++;
        }
    }

    public void AddTo(StateHash hash)
    {
        hash.Add(Year).Add(Step);
    }
}
