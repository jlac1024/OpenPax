using OpenPax.Sim.Combat;
using OpenPax.Sim.Numerics;

namespace OpenPax.Sim;

/// <summary>
/// Explosions and flashes in a battle (docs/sim/combat-step.md).
/// They only show, but the battle cannot end while any is still dying
/// down, so their lifetimes are part of the rules. No draws: where the
/// original picks a kind with its clock mixed generator, the port takes the
/// first of the three (it only changes the picture).
/// </summary>
public sealed partial class Game
{
    private const int MissileBlast = 9;
    private const int HitFlash = 10;
    private const int BandSplit = 131;
    private const double FlashSector = 0.022222222222222223;

    /// <summary>Steps an effect of a kind lasts (the table).</summary>
    internal static int EffectLife(int kind) => kind switch
    {
        >= 11 and <= 130 => 10,
        143 => 39,
        _ => 35,
    };

    /// <summary>An effect at a point, drifting along a heading ( with nothing to follow).</summary>
    private void AddEffect(List<Effect> list, int kind, double x, double y, int heading = 0, double speed = 0.0)
    {
        var effect = new Effect(kind, EffectLife(kind)) { Id = TakeCombatId(), Facing = heading, Speed = speed };
        effect.SetPosition(x, y);
        list.Insert(0, effect);
    }

    /// <summary>An effect riding on an object at an offset from it ( with something to follow).</summary>
    private void AddFollower(List<Effect> list, int kind, BattleObject follow, double offsetX, double offsetY)
    {
        var effect = new Effect(kind, EffectLife(kind))
        {
            Id = TakeCombatId(),
            Follow = follow,
            OffsetX = X87.ToInt(offsetX - -0.5),
            OffsetY = X87.ToInt(offsetY - -0.5),
        };
        effect.SetPosition(follow.X + effect.OffsetX, follow.Y + effect.OffsetY);
        list.Insert(0, effect);
    }

    /// <summary>A ship's explosion: smaller hulls take the later kinds; it drifts on as the ship moved.</summary>
    private void ShipExplosion(Battle battle, BattleShip ship)
    {
        var hull = ship.Ship.Type;
        var kind = hull < 2 ? 6 : hull < 4 ? 3 : 0;
        AddEffect(battle.ShipEffects, kind, ship.PosX, ship.PosY, ship.Facing, ship.Speed);
    }

    /// <summary>An orbital's explosion: by the stack's count at the start.</summary>
    private void OrbitalExplosion(Battle battle, Orbital orbital)
    {
        var kind = orbital.Count < 5 ? 6 : orbital.Count <= 6 ? 3 : 0;
        AddEffect(battle.OrbitalEffects, kind, orbital.PosX, orbital.PosY);
    }

    /// <summary>The sector, 0 to 7, a shot came from, centered on its heading turned about.</summary>
    private static int FlashSide(Shot h)
    {
        var a = h.Facing - 180.0 - -22.5;
        while (a < 0.0)
        {
            a += 360.0;
        }

        while (a >= 360.0)
        {
            a -= 360.0;
        }

        return (byte)(int)(a * FlashSector);
    }

    /// <summary>
    /// A ship's shield flares where a shot struck it: the kind
    /// shows the side, the hull's size and the shield; it rides on the ship.
    /// </summary>
    private void ShieldFlash(Battle battle, BattleShip ship, Shot h)
    {
        var side = FlashSide(h);
        var hull = ship.Ship.Type;
        var size = hull < 2 ? 0 : hull == 2 ? 1 : 2;
        var percent = ship.Ship.Shield?.Tech.ShieldPercent ?? 0;
        var kind = side + 11 + (size + percent % 5 * 3) * 8;
        AddFollower(side > 4 ? battle.ShipEffects : battle.OrbitalEffects, kind, ship, 0.0, 0.0);
    }

    /// <summary>An orbital's shield flares.</summary>
    private void ShieldFlash(Battle battle, Orbital orbital, Shot h)
    {
        var side = FlashSide(h);
        var percent = orbital.Structure.ShieldTech?.ShieldPercent ?? 0;
        var kind = (byte)(side + 16) + 11 + percent % 5 * 24;
        AddFollower(side > 4 ? battle.ShipEffects : battle.OrbitalEffects, kind, orbital, 0.0, 0.0);
    }

    /// <summary>A flash where a shot struck, riding on what it struck.</summary>
    private void StrikeFlash(Battle battle, BattleObject struck, Shot h)
    {
        AddFollower(battle.OrbitalEffects, HitFlash, struck, h.X - struck.X, h.Y - struck.Y);
    }

    /// <summary>
    /// Effects move, the first list then the second: one riding
    /// on something now dead goes at once; the rest keep to what they ride
    /// on, or drift.
    /// </summary>
    private void MoveEffects(Battle battle)
    {
        foreach (var list in (List<Effect>[])[battle.OrbitalEffects, battle.ShipEffects])
        {
            foreach (var effect in list.ToList())
            {
                if (effect.Follow is { } follow)
                {
                    if (follow.Dead)
                    {
                        list.Remove(effect);
                        continue;
                    }

                    effect.SetPosition(follow.X + effect.OffsetX, follow.Y + effect.OffsetY);
                }
                else if (effect.Speed != 0.0)
                {
                    var next = Along(effect, effect.Facing, effect.Speed);
                    effect.SetPosition(next.X, next.Y);
                }
            }
        }
    }

    /// <summary>Effects age, the first list then the second; one goes when its age reaches its life.</summary>
    private static void PostStepEffects(Battle battle)
    {
        foreach (var list in (List<Effect>[])[battle.OrbitalEffects, battle.ShipEffects])
        {
            foreach (var effect in list.ToList())
            {
                effect.Age++;
                if (effect.Age == effect.Life)
                {
                    list.Remove(effect);
                }
            }
        }
    }
}
