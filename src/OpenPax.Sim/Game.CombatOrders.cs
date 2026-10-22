using OpenPax.Sim.Combat;
using OpenPax.Sim.Numerics;

namespace OpenPax.Sim;

/// <summary>Orders in a battle fought step by step: the order message and each ship's order (docs/sim/combat-step.md).</summary>
public sealed partial class Game
{
    private const int SelectionLimit = 150;

    /// <summary>
    /// Runs an order. The selection is read back as the original
    /// reads its message: each ship still in the current battle goes to the
    /// front, so the ships are taken in the reverse of the order given. An
    /// order to an orbital goes to it alone.
    /// </summary>
    public void ApplyBattleOrder(BattleOrder order)
    {
        var selection = new List<BattleShip>();
        if (Battles.Count > 0)
        {
            foreach (var id in order.ShipIds)
            {
                if (FindBattleShip(Battles[0], id) is { } found && selection.Count < SelectionLimit)
                {
                    selection.Insert(0, found);
                }
            }
        }

        if (Battles.FirstOrDefault(b => b.Id == order.BattleId) is not { } battle)
        {
            return;
        }

        if (order.Kind == BattleOrderKind.Horizontal && selection.Count == 0)
        {
            if (FindBattleShip(battle, order.TargetId) is { } own)
            {
                OrderShip(battle, own, order.Kind, null, null);
            }

            return;
        }

        BattleObject? target = order.TargetId == -1 ? null
            : (BattleObject?)FindBattleShip(battle, order.TargetId)
              ?? Enumerable.Range(0, Battle.Players).SelectMany(p => battle.Orbitals[p]).FirstOrDefault(o => o.Id == order.TargetId);
        if (order.OrbitalId != -1)
        {
            // An order naming a target that is gone does nothing.
            var orbital = Enumerable.Range(0, Battle.Players).SelectMany(p => battle.Orbitals[p]).FirstOrDefault(o => o.Id == order.OrbitalId);
            if (orbital is not null && (order.TargetId == -1 || target is not null))
            {
                OrderOrbital(battle, orbital, (int)order.Kind, target);
            }

            return;
        }

        var point = (order.X, order.Y);
        switch (order.Kind)
        {
            case BattleOrderKind.Move:
                GroupMove(selection, point);
                break;
            case BattleOrderKind.Horizontal:
                LineAcross(selection, point);
                break;
            case BattleOrderKind.Vertical:
                LineDown(selection, point);
                break;
            case BattleOrderKind.RightDiagonal or BattleOrderKind.LeftDiagonal:
                Diagonal(selection, point, order.Kind == BattleOrderKind.LeftDiagonal);
                break;
            case BattleOrderKind.Face or BattleOrderKind.Target or BattleOrderKind.Attack or BattleOrderKind.FightersAttack
                or BattleOrderKind.FightersDefend or BattleOrderKind.RecallFighter or BattleOrderKind.ToggleAutoLaunch or BattleOrderKind.Retreat:
                foreach (var ship in selection)
                {
                    OrderShip(battle, ship, order.Kind, point, target);
                }

                break;
            default:
                break;
        }
    }

    /// <summary>Whose an object of the battle is (vt+0x38): orders name ships and orbitals.</summary>
    private static int Owner(BattleObject o) => o switch
    {
        BattleShip s => s.Owner,
        Orbital b => b.Owner,
        Fighter f => f.Owner,
        Mine m => m.Owner,
        Shot s => s.Owner,
        _ => -1,
    };

    private static BattleShip? FindBattleShip(Battle battle, int id)
    {
        for (var p = 0; p < Battle.Players; p++)
        {
            if (battle.Ships[p].FirstOrDefault(s => s.Id == id) is { } ship)
            {
                return ship;
            }
        }

        return null;
    }

    /// <summary>
    /// One ship's order. An order that needs a point or a
    /// target and lacks it does nothing. Both fighter orders send a fighter
    /// at an enemy or to guard a friend, by whose the target is.
    /// </summary>
    internal void OrderShip(Battle battle, BattleShip ship, BattleOrderKind kind, (int X, int Y)? point, BattleObject? target)
    {
        switch (kind)
        {
            case BattleOrderKind.Face when point is { } at:
                ship.State = BattleShip.Turning;
                ship.WantedHeading = X87.ToInt(Direction(ship.X, ship.Y, at.X, at.Y) - -0.5);
                break;
            case BattleOrderKind.Target when target is not null:
                ship.State = BattleShip.Attacking;
                ship.Target = target;
                break;
            case BattleOrderKind.Attack when target is not null:
                ship.State = BattleShip.Pursuing;
                ship.Target = target;
                ship.Detour = null;
                ship.Goal = BattleGoal.Following(target);
                break;
            case BattleOrderKind.FightersAttack or BattleOrderKind.FightersDefend when target is not null:
                if (Owner(target) == ship.Owner)
                {
                    GuardWithFighter(battle, ship, ship.Owner, target);
                }
                else
                {
                    LaunchFromShip(battle, ship, target);
                }

                break;
            case BattleOrderKind.RecallFighter:
                RecallFighter(battle, ship, ship.Owner, skipReturning: true);
                break;
            case BattleOrderKind.ToggleAutoLaunch:
                ship.AutoLaunch = !ship.AutoLaunch;
                break;
            case BattleOrderKind.Retreat:
                PickRetreat(battle, ship);
                break;
            case BattleOrderKind.Horizontal:
                if (battle.Commanders[ship.Owner] is { } commander)
                {
                    SetAuto(battle, commander, !(commander.Computer && commander.Counter >= 7));
                }

                break;
            default:
                break;
        }
    }

    /// <summary>A ship is sent to a point (order 2): any detour dropped.</summary>
    private static void SendTo(BattleShip ship, (int X, int Y) point)
    {
        ship.State = BattleShip.Moving;
        ship.Detour = null;
        ship.Goal = BattleGoal.At(point.X, point.Y);
    }

    /// <summary>
    /// AUTO: the side fights by itself, or stops. Stopping
    /// leaves every ship of the side idle; starting makes it think at once.
    /// </summary>
    private void SetAuto(Battle battle, Commander commander, bool on)
    {
        commander.Computer = on;
        battle.AutoEngaged = on;
        if (!on)
        {
            foreach (var ship in battle.Ships[commander.Player])
            {
                ship.State = BattleShip.Idle;
            }

            return;
        }

        ForceThink(battle, commander);
    }
}
