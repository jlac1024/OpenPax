using OpenPax.Sim.Galaxy;

namespace OpenPax.Sim.Players;

/// <summary>A colony on a planet; see docs/sim/economy.md.</summary>
public sealed class Colony
{
    public const int Credits = 0;
    public const int ResearchResource = 1;
    public const int Espionage = 2;
    public const int Construction = 3;
    public const int Population = 4;
    public const int ImprovementSlots = 50;

    /// <param name="improvementSlots">A slot for each improvement there is: the original's 50, more with mods' (docs/modding/content.md).</param>
    internal Colony(Player owner, Planet planet, int star, int improvementSlots = ImprovementSlots)
    {
        Improvements = new Improvement?[Math.Max(ImprovementSlots, improvementSlots)];
        Owner = owner;
        Planet = planet;
        Star = star;
        Resources[Population] = 1.0;
        MaxPopulation = MaximumPopulation(owner, planet, null);
    }

    public Player Owner { get; }

    public Planet Planet { get; }

    /// <summary>Index of the planet's star.</summary>
    public int Star { get; }

    public int PlanetSlot => Planet.Slot;

    /// <summary>+4, recomputed when a population improvement completes.</summary>
    public int MaxPopulation { get; internal set; }

    /// <summary>Population growth of the last period (+8).</summary>
    public double Growth { get; internal set; }

    /// <summary>Credits, research, espionage, construction and population of the period (+0x10); population is the current one.</summary>
    public double[] Resources { get; } = new double[5];

    public double CurrentPopulation => Resources[Population];

    /// <summary>Per-population factor of each resource (+0x38).</summary>
    public double[] PerPopulation { get; } = [10.0, 0.0, 0.0, 10.0, 0.0];

    /// <summary>Flat amount of each resource (+0x60).</summary>
    public double[] Flat { get; } = new double[5];

    /// <summary>One slot per planetary improvement, by its place among them; null where not available (+0x88).</summary>
    public Improvement?[] Improvements { get; }

    /// <summary>Credits put into construction each period (+0x150).</summary>
    public double Funding { get; internal set; }

    /// <summary>Construction points to spend this period (+0x158).</summary>
    public double ConstructionPoints { get; internal set; }

    /// <summary>Which of the owner's plans orders its improvements (+0x160).</summary>
    public int Plan { get; internal set; }

    /// <summary>Upkeep of this colony's completed research, espionage and construction improvements (+0x180, +0x178, +0x188), by resource.</summary>
    public double[] ImprovementUpkeep { get; } = new double[5];

    /// <summary>Upkeep of this colony's structures (+0x190).</summary>
    public double StructureUpkeep { get; internal set; }

    /// <summary>Structures, the newest kind first (+0x198).</summary>
    public List<Structure> Structures { get; } = [];

    /// <summary>Set once the colony is gone; loops that started before stop working on it.</summary>
    public bool Destroyed { get; internal set; }

    /// <summary>
    /// (rating + 2m) * type + moons * m with m = 1 + habitability, plus the
    /// population improvements' flat amount once the colony exists.
    /// </summary>
    public static int MaximumPopulation(Player player, Planet planet, Colony? colony)
    {
        var m = Habitability(player.Species, planet) + 1;
        var max = (planet.Rating + 2 * m) * planet.Type + planet.Moons * m;
        return colony is null ? max : max + Numerics.X87.ToInt(colony.Flat[Population]);
    }

    /// <summary>1 for a breathable atmosphere, plus 1 for a tolerable temperature.</summary>
    public static int Habitability(Species species, Planet planet)
    {
        return (Breathes(species, planet) ? 1 : 0) + (species.Tolerates(planet.Temperature) ? 1 : 0);
    }

    /// <summary>The original's rule by the planet's class, unless a mod's planet type says who breathes it.</summary>
    public static bool Breathes(Species species, Planet planet) => planet.Breathing switch
    {
        Planet.BreathingAll => true,
        Planet.BreathingNone => false,
        Planet.BreathingAnaerobic => species.Has(Species.Anaerobic),
        _ => species.Breathes(planet.Class),
    };

    /// <summary>The first structure of a kind, or null.</summary>
    public Structure? FirstOfKind(int kind)
    {
        return Structures.FirstOrDefault(s => s.Tech.Kind == kind);
    }

    public void AddTo(StateHash hash)
    {
        hash.Add(Star).Add(PlanetSlot).Add(MaxPopulation).Add(Growth).Add(Funding).Add(ConstructionPoints).Add(Plan).Add(StructureUpkeep);
        foreach (var v in Resources.Concat(PerPopulation).Concat(Flat).Concat(ImprovementUpkeep))
        {
            hash.Add(v);
        }

        foreach (var i in Improvements)
        {
            hash.Add(i?.Built ?? -1.0);
        }

        hash.Add(Structures.Count);
        foreach (var s in Structures)
        {
            s.AddTo(hash);
        }
    }
}
