namespace OpenPax.Sim.Combat;

/// <summary>What an order in a battle asks (the order message's kind).</summary>
public enum BattleOrderKind
{
    /// <summary>The selection moves as a group to a point.</summary>
    Move = 1,

    /// <summary>Each turns to face a point.</summary>
    Face = 2,

    /// <summary>Each faces the target and fires at it, without moving.</summary>
    Target = 3,

    /// <summary>Each chases the target to range.</summary>
    Attack = 4,

    /// <summary>Fighters go for the target (an enemy) or guard it (a friend).</summary>
    FightersAttack = 5,

    FightersDefend = 6,

    /// <summary>One fighter of each ship comes home.</summary>
    RecallFighter = 7,

    /// <summary>Each ship's launching of fighters by itself is turned over.</summary>
    ToggleAutoLaunch = 8,

    /// <summary>A line across; with no ships selected, AUTO for the side.</summary>
    Horizontal = 9,

    Vertical = 10,

    RightDiagonal = 11,

    LeftDiagonal = 12,

    /// <summary>Each leaves the field by the safest way.</summary>
    Retreat = 13,
}
