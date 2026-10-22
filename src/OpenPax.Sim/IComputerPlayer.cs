using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// The computer player modules (OpenPax.Ai). The AI task picks one
/// computer player per run and hands it here with the task's period.
/// </summary>
public interface IComputerPlayer
{
    void Think(Game game, Player player, int period);
}
