namespace OpenPax.Data.Tables.Tech;

/// <summary>
/// Class 6009, read: ship specials and planetary
/// installations (repair bays, colonizers, minesweepers, shipyards, missile
/// bases, battle stations, fighter bases, minefields, academies).
/// </summary>
/// <param name="Level">Installation level 1 to 5, or repair amount, or minefield strength.</param>
/// <param name="Strength">100 or 500 for installations, else 0.</param>
public sealed record Installation(int Level, int Strength) : TechDetails;
