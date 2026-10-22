namespace OpenPax.Data.Tables;

/// <summary>What the simulation needs to know of the Image/Combat.img pictures, without the art.</summary>
/// <param name="Rows">Each picture's last visible row (orbital footprints).</param>
/// <param name="Sizes">Each picture's width and height (where a planet's blocks stand).</param>
public sealed record CombatSprites(Dictionary<int, int> Rows, Dictionary<int, (int Width, int Height)> Sizes);
