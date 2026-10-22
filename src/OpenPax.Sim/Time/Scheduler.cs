namespace OpenPax.Sim.Time;

/// <summary>The original's task list: periodic work counted in ticks.</summary>
public sealed class Scheduler
{
    private readonly List<ScheduledTask> _tasks;

    private Scheduler(List<ScheduledTask> tasks)
    {
        _tasks = tasks;
    }

    /// <summary>Tasks in the order they run each tick.</summary>
    public IReadOnlyList<ScheduledTask> Tasks => _tasks;

    /// <summary>
    /// The tasks a game starts with. The original pushes each one on the
    /// front of the list, so they run in the reverse of the order added;
    /// this list is already in run order.
    /// </summary>
    // task periods from the constructors  to
    public static Scheduler ForNewGame(int difficulty, bool networked)
    {
        var tasks = new List<ScheduledTask>();
        if (!networked)
        {
            tasks.Add(new ScheduledTask(TaskKind.Autosave, 4000));
        }

        tasks.Add(new ScheduledTask(TaskKind.Empire, 40));
        tasks.Add(new ScheduledTask(TaskKind.Espionage, 40));
        tasks.Add(new ScheduledTask(TaskKind.Research, 40));
        tasks.Add(new ScheduledTask(TaskKind.Diplomacy, 200));
        tasks.Add(new ScheduledTask(TaskKind.Advisors, 2000));
        tasks.Add(new ScheduledTask(TaskKind.PlayerUpdate, 200));
        tasks.Add(new ScheduledTask(TaskKind.Calendar, 4));
        tasks.Add(new ScheduledTask(TaskKind.ComputerPlayer, ComputerPlayerPeriod(difficulty)));
        tasks.Add(new ScheduledTask(TaskKind.StarSystems, 1));
        return new Scheduler(tasks);
    }

    /// <summary>Easier games give computer players fewer chances to think.</summary>
    public static int ComputerPlayerPeriod(int difficulty)
    {
        return difficulty switch
        {
            1 => 20,
            2 => 10,
            _ => 5,
        };
    }

    /// <summary>
    /// One tick: every task counts down, and each that reaches zero runs.
    /// A task that returns false is taken off the list, as in the original;
    /// none of the original's tasks do.
    /// </summary>
    internal void Tick(Func<TaskKind, bool> run)
    {
        for (var i = 0; i < _tasks.Count; i++)
        {
            var task = _tasks[i];
            task.Countdown--;
            if (task.Countdown != 0)
            {
                continue;
            }

            if (run(task.Kind))
            {
                task.Countdown = task.Period;
            }
            else
            {
                _tasks.RemoveAt(i);
                i--;
            }
        }
    }

    public void AddTo(StateHash hash)
    {
        hash.Add(_tasks.Count);
        foreach (var task in _tasks)
        {
            hash.Add((int)task.Kind).Add(task.Countdown);
        }
    }
}
