namespace OpenPax.Data.Tables.Tech;

/// <summary>Class 6008, read.</summary>
/// <param name="MaxHullClass">2 to 5: the largest hull class the armor fits (shown as Max Hull).</param>
/// <param name="StructureMultiplier">1.0 to 1.8; multiplies a design's structure.</param>
public sealed record Armor(byte MaxHullClass, double StructureMultiplier) : TechDetails;
