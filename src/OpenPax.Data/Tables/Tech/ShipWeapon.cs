namespace OpenPax.Data.Tables.Tech;

/// <summary>Class 6005: beams, projectiles, missiles, planet bombs, area weapons. Read.</summary>
/// <param name="Effect">0 to 44: which picture set and sound its shots have in battle (the table).</param>
/// <param name="Secondary">Missile hit points or bomb strength; 0 for beams. Meaning uncertain.</param>
/// <param name="DamageByRange">Damage in range bands 1 to 4. The last non-zero band sets the maximum range: 300, 500, 750 or 1500.</param>
/// <param name="Accuracy">0 to 30: the accuracy bonus in percent, shown "+N%".</param>
/// <param name="Reload">3 to 30, lower for better weapons. The design builder divides damage by it; almost certainly the delay between shots.</param>
/// <param name="AreaRadius">50 to 65 for area weapons, else 0.</param>
/// <param name="Unknown5C">5 to 25; meaning uncertain.</param>
/// <param name="Unknown60">Always 0.</param>
/// <param name="Unknown61">Always 0.</param>
/// <param name="Tracking">255 for weapons that do not track (shown "No"); 0 to 100 for missiles, probably their tracking strength.</param>
/// <param name="WeaponType">0 normal, 1 planet bomb, 2 area weapon.</param>
public sealed record ShipWeapon(
    int Effect,
    int Secondary,
    IReadOnlyList<short> DamageByRange,
    byte Accuracy,
    int Reload,
    int AreaRadius,
    int Unknown5C,
    byte Unknown60,
    byte Unknown61,
    byte Tracking,
    byte WeaponType) : TechDetails;
