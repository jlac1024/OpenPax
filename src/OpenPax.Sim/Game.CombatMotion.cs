using OpenPax.Sim.Combat;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Numerics;

namespace OpenPax.Sim;

/// <summary>
/// How ships move in a battle fought step by step (docs/sim/combat-step.md):
/// a ship turns on the spot, then goes at full speed along its heading; one
/// held up waits, then finds a way round.
/// </summary>
public sealed partial class Game
{
    private const double FieldSize = 3600.0;

    private static double DirectionD(double ax, double ay, double bx, double by) => X87.DirectionDegrees(bx - ax, by - ay);

    private static double DistanceD(double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return Math.Sqrt(dy * dy + dx * dx);
    }

    private static double DistanceD(BattleObject a, (double X, double Y) b) => DistanceD(a.PosX, a.PosY, b.X, b.Y);

    private static int WrapDegrees(int h)
    {
        while (h > 359)
        {
            h -= 360;
        }

        while (h < 0)
        {
            h += 360;
        }

        return h;
    }

    /// <summary>The point at a distance along a heading from an object's exact position.</summary>
    private (double X, double Y) Along(BattleObject o, int heading, double distance)
    {
        return (Tables.Cos[heading] * distance + o.PosX, Tables.Sin[heading] * distance + o.PosY);
    }

    private static int Half(BattleShip ship) => (ship.Radius + 1) / 2;

    private static void MarkShip(Battle battle, BattleShip ship) => battle.MarkFootprint(ship.X, ship.Y, Half(ship), ship.Id);

    /// <summary>
    /// One step of turning toward the wanted heading; true once
    /// it faces it. Quirk kept: the step is floored, so turning left takes a
    /// degree more than turning right (11.25 gives +11 and -12). A wanted
    /// heading of 360 is never reached.
    /// </summary>
    internal static bool Turn(BattleShip ship)
    {
        var diff = ship.WantedHeading - ship.Facing;
        if (diff < 0)
        {
            diff += 360;
        }

        var dir = 1;
        if (diff > 180)
        {
            diff = 360 - diff;
            dir = -1;
        }

        var step = diff >= ship.TurnRate ? X87.ToInt(dir * ship.TurnRate) : dir * diff;
        ship.Facing = WrapDegrees(ship.Facing + step);
        var v = ship.Facing * (32.0 / 360.0);
        var frac = v - X87.ToInt(v);
        if (diff > 1 || frac < 0.25 || frac > 0.75)
        {
            ship.UpdateSpriteFrame();
        }

        return ship.Facing == ship.WantedHeading;
    }

    /// <summary>
    /// Whether the ship would fit at a point: inside the field by
    /// half its size, and no cell of its footprint there taken by anything
    /// else. Its own footprint is lifted and put back to look.
    /// </summary>
    private static bool HasRoom(Battle battle, BattleShip ship, (double X, double Y) at)
    {
        var h = (double)((ship.Radius >> 1) + 1);
        if (at.X < h || at.X > FieldSize - h || at.Y < h || at.Y > FieldSize - h)
        {
            return false;
        }

        var ix = X87.ToInt(at.X - -0.5);
        var iy = X87.ToInt(at.Y - -0.5);
        battle.ClearFootprint(ship.X, ship.Y, Half(ship));
        var taken = battle.Taken(ix, iy, Half(ship));
        MarkShip(battle, ship);
        return taken == 0;
    }

    /// <summary>
    /// One step toward the detour, else the goal; true when the
    /// goal is reached. A pursuer keeps up to 10 degrees of error rather than
    /// stop to turn; others turn on the spot first. No acceleration: a ship
    /// that moves moves at full speed.
    /// </summary>
    private bool MoveToGoal(Battle battle, BattleShip ship)
    {
        // A pursuer whose goal went in the last step: the original would read
        // nothing here and fail; the port stands still instead.
        if ((ship.Detour ?? ship.Goal) is not { } goal)
        {
            ship.Speed = 0.0;
            return false;
        }

        var onDetour = ship.Detour is not null;
        var p = goal.Point;
        ship.WantedHeading = X87.ToInt(DirectionD(ship.PosX, ship.PosY, p.X, p.Y) - -0.5);
        if (ship.State == BattleShip.Pursuing)
        {
            var delta = (double)(ship.WantedHeading - ship.Facing);
            if (delta > 10.0 || delta < -10.0)
            {
                Turn(ship);
                return false;
            }
        }
        else if (!Turn(ship))
        {
            return false;
        }

        ship.Speed = ship.TopSpeed;
        var next = Along(ship, ship.Facing, ship.Speed);
        var d = DistanceD(ship, p);
        if (!HasRoom(battle, ship, next))
        {
            ship.Speed = 0.0;
            ship.Blocked = (ship.Blocked + 1) & 0xFF;
            if (ship.Blocked < 5 || onDetour)
            {
                if (onDetour)
                {
                    PlanDetour(battle, ship);
                }

                return false;
            }

            // Held up long enough on the way to the goal: close enough when
            // the goal itself has no room and the ship is within its size.
            if (!HasRoom(battle, ship, p) && d < ship.Radius)
            {
                ship.WantedHeading = X87.ToInt(DirectionD(ship.PosX, ship.PosY, p.X, p.Y) - -0.5);
                ship.Speed = 0.0;
                ship.Goal = null;
                return true;
            }

            PlanDetour(battle, ship);
            return false;
        }

        ship.Blocked = 0;
        battle.ClearFootprint(ship.X, ship.Y, Half(ship));
        if (d < ship.Speed)
        {
            ship.SetPosition(p.X, p.Y);
            if (!onDetour)
            {
                ship.Speed = 0.0;
                ship.Goal = null;
                return true;
            }

            PlanDetour(battle, ship);
            return false;
        }

        ship.SetPosition(next.X, next.Y);
        if (onDetour)
        {
            PlanDetour(battle, ship);
        }

        return false;
    }

    /// <summary>
    /// The way round: none needed when three points ahead
    /// toward the goal are free; else the first free heading fanned out 5
    /// degrees at a time within 70 degrees, else straight ahead if free, else
    /// the fan within 180 degrees.
    /// </summary>
    private void PlanDetour(Battle battle, BattleShip ship)
    {
        if (ship.Goal is not { } goal)
        {
            ship.Detour = null;
            return;
        }

        var p = goal.Point;
        var h = X87.ToInt(DirectionD(ship.PosX, ship.PosY, p.X, p.Y));
        var top = ship.TopSpeed;
        if (HasRoom(battle, ship, Along(ship, h, top)) && HasRoom(battle, ship, Along(ship, h, top))
            && HasRoom(battle, ship, Along(ship, h, top + top)))
        {
            ship.Detour = null;
            ship.Speed = 0.0;
            return;
        }

        if (Fan(battle, ship, h, 70))
        {
            return;
        }

        var ahead = Along(ship, ship.Facing, top);
        if (HasRoom(battle, ship, ahead))
        {
            SetDetour(ship, ahead);
            return;
        }

        Fan(battle, ship, h, 180);
    }

    /// <summary>
    /// Headings either side of h, 5 degrees apart out to the span:
    /// the first with four free points ahead (at 1, 2, 3 and 4 times the
    /// speed) gives a detour at the ship's size along it.
    /// </summary>
    private bool Fan(Battle battle, BattleShip ship, int h, int span)
    {
        var top = ship.TopSpeed;
        for (var k = 1; k <= span / 5; k++)
        {
            foreach (var sign in (int[])[-1, 1])
            {
                var a = WrapDegrees(h + 5 * k * sign);
                if (HasRoom(battle, ship, Along(ship, a, top)) && HasRoom(battle, ship, Along(ship, a, top + top))
                    && HasRoom(battle, ship, Along(ship, a, top * 3.0)) && HasRoom(battle, ship, Along(ship, a, top * 4.0)))
                {
                    SetDetour(ship, Along(ship, a, ship.Radius));
                    return true;
                }
            }
        }

        return false;
    }

    private static void SetDetour(BattleShip ship, (double X, double Y) at)
    {
        if (ship.Detour is { } detour)
        {
            detour.X = at.X;
            detour.Y = at.Y;
        }
        else
        {
            ship.Detour = BattleGoal.At(at.X, at.Y);
        }
    }

    /// <summary>Near enough the edge to leave: within 1.5 times its size.</summary>
    private static bool AtEdge(BattleShip ship)
    {
        var m = ship.Radius * 1.5;
        return ship.X < m || ship.X > FieldSize - m || ship.Y < m || ship.Y > FieldSize - m;
    }

    /// <summary>
    /// A ship's step: its order state drives it, then its
    /// footprint is marked where it now stands. A computer ship under way
    /// first stops on seeing an enemy mine near.
    /// </summary>
    internal void MoveShip(Battle battle, BattleShip ship)
    {
        MineStop(battle, ship);
        switch (ship.State)
        {
            case BattleShip.Turning:
                if (Turn(ship))
                {
                    ship.State = BattleShip.Idle;
                }

                break;
            case BattleShip.Moving:
                if (MoveToGoal(battle, ship))
                {
                    ship.State = BattleShip.Idle;
                }

                break;
            case BattleShip.Attacking:
                if (ship.Target is not { } target)
                {
                    ship.State = BattleShip.Idle;
                    return;
                }

                ship.WantedHeading = X87.ToInt(Direction(ship.X, ship.Y, target.X, target.Y) - -0.5);
                if (ship.Facing != ship.WantedHeading)
                {
                    Turn(ship);
                }

                break;
            case BattleShip.Pursuing:
                if (ship.Target is not { } quarry)
                {
                    ship.State = BattleShip.Idle;
                    return;
                }

                Pursue(battle, ship, quarry);
                break;
            case BattleShip.Leaving:
                if (AtEdge(ship))
                {
                    LeaveField(battle, ship);
                    return;
                }

                LeaveCounter++;
                if (LeaveCounter > 100)
                {
                    LeaveCounter = 0;
                    PickRetreat(battle, ship);
                }

                MoveToGoal(battle, ship);
                break;
            default:
                break;
        }

        MarkShip(battle, ship);
    }

    /// <summary>
    /// A pursuer closes to 260 of its goal (300 when standing, 300 less its
    /// size against a planet it can bombard), then stops and faces its target.
    /// </summary>
    private void Pursue(Battle battle, BattleShip ship, BattleObject target)
    {
        var p = ship.Goal?.Point ?? (target.PosX, target.PosY);
        var range = ship.Speed <= 0.0 ? 300 : 260;
        if (target is PlanetBlock && ship.Ship.CanAttackPlanets)
        {
            range = 300 - ship.Radius;
        }

        if (DistanceD(ship, p) <= range)
        {
            ship.WantedHeading = X87.ToInt(Direction(ship.X, ship.Y, target.X, target.Y) - -0.5);
            if (ship.Facing != ship.WantedHeading)
            {
                Turn(ship);
            }

            ship.Speed = 0.0;
            return;
        }

        MoveToGoal(battle, ship);
    }

    /// <summary>
    /// A ship that reached the edge leaves (state 8): its footprint is
    /// cleared, then it goes as a retreating ship does (the fleet retreats,
    /// orders dropped, a new orbit drawn). Its docked fighters go with it;
    /// those out fly on with no carrier.
    /// </summary>
    private void LeaveField(Battle battle, BattleShip ship)
    {
        battle.ClearFootprint(ship.X, ship.Y, Half(ship));
        CarrierGone(battle, ship);
        Leave(battle, ship);
    }

    /// <summary>
    /// The retreat order: the way out with the least threat.
    /// Each enemy ship and defending orbital adds its weight to the 30 degree
    /// sector it lies in and less to those beside it; a planet weighs toward
    /// the field's far corner. Ties go to the nearer exit. No draws.
    /// </summary>
    internal void PickRetreat(Battle battle, BattleShip ship)
    {
        ship.State = BattleShip.Leaving;
        var c = new int[12];
        void Spread(int k, int w, int a, int b, int e)
        {
            c[k] += w;
            c[(k + 1) % 12] += a;
            c[(k + 11) % 12] += a;
            c[(k + 2) % 12] += b;
            c[(k + 10) % 12] += b;
            c[(k + 3) % 12] += e;
            c[(k + 9) % 12] += e;
        }

        if (battle.Planet is not null)
        {
            var d = Orbit.Wrap(Direction(ship.X, ship.Y, 3600, 3600) - -15.0);
            Spread(X87.ToInt(d * (1.0 / 30.0) - -0.5) % 12, 10, 5, 3, 3);
        }

        for (var q = 0; q < Battle.Players; q++)
        {
            if (q == ship.Owner || !AtWar(ship.Owner, q))
            {
                continue;
            }

            foreach (var enemy in battle.Ships[q].Where(e => !e.Dead))
            {
                Weigh(enemy.X, enemy.Y, enemy.Ship.Type + 2);
            }

            foreach (var orbital in battle.Orbitals[q].Where(o => !o.Dead && o.Structure.Tech.Kind is 9 or 7 or 8))
            {
                Weigh(orbital.X, orbital.Y, orbital.Count * 3);
            }
        }

        void Weigh(int x, int y, int w)
        {
            var d = Orbit.Wrap(Direction(ship.X, ship.Y, x, y) - -15.0);
            Spread(X87.ToInt(d * (1.0 / 30.0)), w, X87.ToInt(w * 0.5 - -0.5), X87.ToInt(w * (1.0 / 3.0) - -0.5), X87.ToInt(w * 0.25 - -0.5));
        }

        var best = 0;
        for (var i = 1; i < 12; i++)
        {
            if (c[i] < c[best])
            {
                best = i;
            }
            else if (c[i] == c[best] && DistanceD(ship, ExitPoint(ship, 30 * i)) < DistanceD(ship, ExitPoint(ship, 30 * best)))
            {
                best = i;
            }
        }

        var exit = ExitPoint(ship, 30 * best);
        ship.Detour = null;
        ship.Goal = BattleGoal.At(X87.ToInt(exit.X - -0.5), X87.ToInt(exit.Y - -0.5));
    }

    /// <summary>Where a line from the ship at an angle meets the field's edge.</summary>
    private (double X, double Y) ExitPoint(BattleShip ship, int a)
    {
        var x0 = (double)ship.X;
        var y0 = (double)ship.Y;
        var cos = Tables.Cos[a];
        var sin = Tables.Sin[a];
        var m = a switch
        {
            0 or 180 => 0.0,
            90 => 1e6,
            270 => -1e6,
            _ => sin / cos,
        };
        var b = y0 - x0 * m;
        var yRight = b - m * -3600.0;
        var xBottom = (3600.0 - b) / m;
        var xTop = (b * -1.0) / m;
        static bool In(double v) => v >= 0.0 && v <= FieldSize;
        if (In(yRight) && cos > 0.0)
        {
            return (FieldSize, yRight);
        }

        if (In(b) && cos < 0.0)
        {
            return (0.0, b);
        }

        if (In(xBottom) && sin > 0.0)
        {
            return (xBottom, FieldSize);
        }

        if (In(xTop) && sin < 0.0)
        {
            return (xTop, 0.0);
        }

        // Every multiple of 30 meets an edge with the game's tables; this is not reached.
        return (x0, y0);
    }
}
