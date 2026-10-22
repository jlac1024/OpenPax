namespace OpenPax.Data.Tables.Tech;

/// <summary>Class 6001, read.</summary>
/// <param name="Rating">2, 4, 6 or 8: wormhole jumps a ship can make away from supply, not a speed.</param>
/// <param name="MaxHullClass">Largest hull the drive moves: 2, 3 or 5.</param>
public sealed record StarDrive(int Rating, byte MaxHullClass) : TechDetails;
