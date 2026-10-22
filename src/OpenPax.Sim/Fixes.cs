namespace OpenPax.Sim;

/// <summary>
/// Fixes to the original's bugs a game may play with (docs/sim/fix-candidates.md),
/// each Jeff's pick, all off by default: with none, the game plays as the
/// original. A game carries its own set, chosen when it starts; it is saved
/// with the game and sent to network clients with its state.
/// </summary>
[Flags]
public enum Fixes
{
    None = 0,

    /// <summary>Ships given to or stolen by a computer player reach it, not vanish.</summary>
    ComputerKeepsShips = 1 << 0,

    /// <summary>Stop Work scraps only a structure still being built, not one damaged in battle.</summary>
    ScrapUnderWork = 1 << 1,

    /// <summary>Work left over when a ship finishes reaches the next one as construction.</summary>
    ShipyardOverflow = 1 << 2,

    /// <summary>A fleet near a fleet battle joins at its own free slot, rather than being left out.</summary>
    FleetsJoinBattles = 1 << 3,

    /// <summary>Fleets near a planet battle join by their terms with each side, the side they are on terms with.</summary>
    PlanetBattleSides = 1 << 4,

    /// <summary>A shot that would step over a planet's block hits it.</summary>
    ShotsHitPlanets = 1 << 5,

    /// <summary>Shots aim where they meet a moving target, not two steps past.</summary>
    ShotsLeadTrue = 1 << 6,

    /// <summary>A battle's experience is given once, not twice.</summary>
    ExperienceOnce = 1 << 7,

    /// <summary>A refit can be ordered only at the player's own shipyard, where it starts, not accepted at a foreign one and never begun.</summary>
    OwnYardRefits = 1 << 8,

    /// <summary>The colonize key needs a colony ship in a fleet at the planet.</summary>
    ColonizeNeedsShip = 1 << 9,

    /// <summary>A trade of more ships than a fleet holds fills a new fleet for the rest, rather than losing them.</summary>
    TradeKeepsAllShips = 1 << 10,

    /// <summary>An empire that never met the victim of an attack does not hold it against the attacker.</summary>
    StrangersIndifferent = 1 << 11,
}
