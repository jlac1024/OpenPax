using OpenPax.Sim.Players;
using OpenPax.Sim.Time;

namespace OpenPax.Sim;

/// <summary>
/// The scheduler's task bodies (docs/sim/time.md). Tasks whose systems are
/// not ported yet run but change nothing; each names the original it will
/// replace. Every original task returns true, so all of these do.
/// </summary>
public sealed partial class Game
{
    private bool RunTask(TaskKind kind, ref StepEvents events)
    {
        switch (kind)
        {
            case TaskKind.Autosave:
                // the save itself is the host's job.
                events |= StepEvents.Autosave;
                break;
            case TaskKind.Calendar:
                Calendar.Advance();
                break;
            case TaskKind.ComputerPlayer:
                ComputerPlayerTurn();
                break;
            case TaskKind.Empire:
                RunEmpireTask(ref events);
                break;
            case TaskKind.Research:
                RunResearchTask();
                break;
            case TaskKind.Diplomacy:
                RunDiplomacyTask();
                break;
            case TaskKind.Espionage:
                RunEspionageTask(ref events);
                break;
            case TaskKind.Advisors:
                RunAdvisorsTask();
                break;
            case TaskKind.PlayerUpdate:
                RecordHistory(ref events);
                break;
            case TaskKind.StarSystems:
                StepStarSystems(ref events);
                break;
        }

        return true;
    }

    /// <summary>
    /// Picks the next computer player still in the game, round robin from
    /// the cursor, which ends just past the one picked, and lets it think
    /// (OpenPax.Ai through <see cref="IComputerPlayer"/>).
    /// </summary>
    private void ComputerPlayerTurn()
    {
        LastThinker = -1;
        if (!Players.Any(p => p.IsAi && !p.Eliminated))
        {
            return;
        }

        Player? picked = null;
        while (picked is null)
        {
            var candidate = Players[ComputerPlayerCursor];
            if (candidate.IsAi && !candidate.Eliminated)
            {
                picked = candidate;
            }

            ComputerPlayerCursor++;
            if (ComputerPlayerCursor >= Players.Count)
            {
                ComputerPlayerCursor = 0;
            }
        }

        LastThinker = picked.Index;
        var period = Scheduler.Tasks.First(t => t.Kind == TaskKind.ComputerPlayer).Period;
        ComputerPlayers?.Think(this, picked, period);
    }
}
