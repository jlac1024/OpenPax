namespace OpenPax.Sim.Players;

/// <summary>
/// A species as the simulation uses it (the original's race template; see
/// docs/formats/tables.md). The caller converts the data records.
/// </summary>
public sealed record Species(
    int RaceId,
    string Name,
    int Picture,
    int PreferredTemperature,
    int TemperatureTolerance,
    IReadOnlyList<bool> Atmospheres,
    IReadOnlyList<bool> PhysicalTraits,
    IReadOnlyList<bool> CulturalTraits,
    IReadOnlyList<sbyte> RateModifiers,
    IReadOnlyList<sbyte> ResearchLevels)
{
    public const int Anaerobic = 0;
    public const int Subterranean = 1;
    public const int Psychic = 2;
    public const int Predator = 3;
    public const int Aquatic = 4;

    public const int Warrior = 0;
    public const int Scientist = 1;
    public const int Politician = 2;
    public const int Spy = 3;
    public const int Builder = 4;
    public const int Nomad = 5;
    public const int Merchant = 6;

    public bool Has(int physicalTrait) => PhysicalTraits[physicalTrait];

    public bool Is(int culturalTrait) => CulturalTraits[culturalTrait];

    /// <summary>Whether the species can live in an atmosphere class.</summary>
    public bool Breathes(int atmosphereClass)
    {
        return Has(Anaerobic) || (atmosphereClass is >= 0 and < 4 && Atmospheres[atmosphereClass]);
    }

    /// <summary>Whether a temperature suits the species.</summary>
    public bool Tolerates(int temperature)
    {
        return Has(Subterranean)
            || (temperature >= PreferredTemperature - TemperatureTolerance && temperature <= PreferredTemperature + TemperatureTolerance);
    }
}
