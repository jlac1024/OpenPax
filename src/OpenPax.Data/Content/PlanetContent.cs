namespace OpenPax.Data.Content;

/// <summary>A new planet type (docs/modding/content.md).</summary>
/// <param name="Class">Its class number, after the original's six (0 to 4 atmospheres, 5 gas giants) and earlier packs'.</param>
/// <param name="Key">Its name in content files.</param>
/// <param name="Name">As the screens show it.</param>
/// <param name="Like">The original atmosphere class (0 to 4) it looks like, in every picture.</param>
/// <param name="Weight">How often it is drawn among rocky planets: 1 is as often as each original class.</param>
/// <param name="Breathable">Who breathes it: "like" (as the class it looks like), "all", "none" or "anaerobic" (only anaerobic species).</param>
/// <param name="RatingBonus">Added to the planet's rating, which feeds its population and wealth.</param>
public sealed record PlanetContent(int Class, string Key, string Name, int Like, double Weight, string Breathable, int RatingBonus);
