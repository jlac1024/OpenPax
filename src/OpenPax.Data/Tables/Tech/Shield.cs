namespace OpenPax.Data.Tables.Tech;

/// <summary>
/// Class 6006: screens, shields and fields, read. In the file
/// <see cref="IsField"/> comes before <see cref="Regeneration"/>, although
/// the original stores them the other way round in memory.
/// </summary>
/// <param name="ShieldType">0 screen, 1 shield, 2 field.</param>
/// <param name="Capacity">Damage stopped before it fails.</param>
/// <param name="Percentereduction">Damage reduction in percent.</param>
/// <param name="IsField">1 for fields.</param>
/// <param name="Regeneration">Capacity restored per combat round.</param>
public sealed record Shield(int ShieldType, int Capacity, int Percentereduction, byte IsField, int Regeneration) : TechDetails;
