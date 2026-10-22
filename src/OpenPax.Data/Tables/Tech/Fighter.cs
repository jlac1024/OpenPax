namespace OpenPax.Data.Tables.Tech;

/// <summary>Class 6007: carrier fighters, read. Seven int16.</summary>
/// <param name="Speed">6 to 10 (shown as Speed).</param>
/// <param name="Weapon1">Tech id of the fighter's weapon.</param>
/// <param name="Weapon2">Tech id of a second weapon, or 0.</param>
/// <param name="Defense">30 to 75 (shown as Defense).</param>
/// <param name="HitPoints">Always equals the base rating (shown as Hit Points).</param>
/// <param name="Accuracy">25 to 70: the accuracy of its shots in battle.</param>
/// <param name="MissileDefense">5 to 50: taken off a missile's tracking when it rolls to hit.</param>
public sealed record Fighter(short Speed, short Weapon1, short Weapon2, short Defense, short HitPoints, short Accuracy, short MissileDefense) : TechDetails;
