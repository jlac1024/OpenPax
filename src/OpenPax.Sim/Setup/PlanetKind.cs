namespace OpenPax.Sim.Setup;

/// <summary>
/// A planet type a mod adds (docs/modding/content.md), drawn alongside the
/// original's atmosphere classes while a galaxy is generated. Such a planet
/// looks like, and is treated in battle and on the screens as, the class it
/// is like; who can breathe it is its own.
/// </summary>
/// <param name="Like">The original atmosphere class (0 to 4) it looks like; the planet's Class.</param>
/// <param name="Weight">How often it is drawn among rocky planets: 1 is as often as each original class.</param>
/// <param name="Breathing">Who breathes it: Planet.BreathingLike, BreathingAll, BreathingNone or BreathingAnaerobic.</param>
/// <param name="RatingBonus">Added to the planet's rating.</param>
public sealed record PlanetKind(string Key, string Name, int Like, double Weight, int Breathing, int RatingBonus);
