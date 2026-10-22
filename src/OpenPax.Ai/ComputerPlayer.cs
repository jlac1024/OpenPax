using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace OpenPax.Ai;

/// <summary>
/// The computer player modules, run for one player per AI task
/// (docs/ai/computer-players.md). None keeps state between runs: all it
/// uses lives on the player and colonies.
/// </summary>
public sealed class ComputerPlayer : IComputerPlayer
{
    public void Think(Game game, Player player, int period)
    {
        var budget = new Budget(game, player, Budget.ReserveFor(game, player));

        Advisors.Run(game, player);
        Treaties.Run(game, player, period);

        Spending.ClearFunding(player);
        Building.Run(game, player, budget);

        ColonyShips.Colonize(game, player);
        ColonyShips.Send(game, player);

        // The colony ship cap is the difficulty when the player was made (+0xFE0 +8).
        ShipBuilding.Run(game, player, budget, game.Setup.Difficulty);

        Espionage.Run(game, player);
        WarFleets.Run(game, player);
        Spending.Fund(game, player);
        Spending.SetPercents(game, player);
    }
}
