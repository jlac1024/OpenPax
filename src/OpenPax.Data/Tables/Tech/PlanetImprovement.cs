namespace OpenPax.Data.Tables.Tech;

/// <summary>
/// Class 6010, read: planetary improvements. Five pairs of
/// doubles, one pair per resource (credits, research, espionage,
/// construction, population), read as per-population then flat. Only
/// one resource per improvement is used (sim/economy.md); for population
/// the flat value raises the maximum and the per-population value the growth.
/// </summary>
public sealed record PlanetImprovement(IReadOnlyList<double> PerPopulation, IReadOnlyList<double> Flat) : TechDetails
{
    public const int Credits = 0;
    public const int Research = 1;
    public const int Espionage = 2;
    public const int Construction = 3;
    public const int Population = 4;
}
