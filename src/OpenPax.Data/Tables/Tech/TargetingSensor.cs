namespace OpenPax.Data.Tables.Tech;

/// <summary>Class 6003, read.</summary>
public sealed record TargetingSensor(short Range, short AccuracyBonus, short TargetsTracked) : TechDetails;
