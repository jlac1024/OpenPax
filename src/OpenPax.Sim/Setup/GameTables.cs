using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim.Setup;

/// <summary>
/// Game data a new game reads, supplied by the caller from the data files
/// so the simulation does not depend on file formats.
/// </summary>
/// <param name="Species">The RACE records in resource order; AIs pick by index.</param>
/// <param name="AiNames">STR# 46000, 30 computer player names.</param>
/// <param name="FleetNameText">STR# 108 entry 10, put between the star name and the fleet number.</param>
/// <param name="Techs">Every technology, by id order.</param>
/// <param name="Hulls">Hulls by type 0 to 5.</param>
/// <param name="DesignNames">STR# 26000; a design of type t is named by entry t + 27.</param>
/// <param name="ClassNames">STR# 20005; entry t names the class of a type t design.</param>
/// <param name="Sin">Sine of each whole degree as the original's table holds it (docs/sim/movement.md).</param>
/// <param name="Cos">Cosine likewise.</param>
/// <param name="StolenFleetName">STR# 112 entry 6, the name of a fleet of stolen ships.</param>
/// <param name="TradedFleetName">STR# 82 entry 7, the name of a fleet of ships received in trade.</param>
/// <param name="AbandonFleetName">STR# 82 entry 6, the name of the fleet a nomad species leaves an abandoned colony in.</param>
/// <param name="TransportName">STR# 100 entry 2, the name of a colony ship design made for abandoning a colony.</param>
/// <param name="CombatSpriteRows">Last visible row of each Image/Combat.img picture: the size of orbital defenses in battle.</param>
public sealed record GameTables(
    IReadOnlyList<Species> Species,
    IReadOnlyList<string> AiNames,
    NameTables GalaxyNames,
    string FleetNameText,
    IReadOnlyList<TechInfo> Techs,
    IReadOnlyList<HullInfo> Hulls,
    IReadOnlyList<string> DesignNames,
    IReadOnlyList<string> ClassNames,
    IReadOnlyList<double> Sin,
    IReadOnlyList<double> Cos,
    IReadOnlyDictionary<int, int> CombatSpriteRows,
    string StolenFleetName,
    string TradedFleetName,
    string AbandonFleetName,
    string TransportName)
{
    /// <summary>Width and height of each Image/Combat.img picture: where a planet's blocks stand in battle.</summary>
    public IReadOnlyDictionary<int, (int Width, int Height)> CombatPictureSizes { get; init; } = new Dictionary<int, (int Width, int Height)>();

    /// <summary>Planet types mods add (docs/modding/content.md); a planet's Kind is its place here plus one. Empty without such mods.</summary>
    public IReadOnlyList<PlanetKind> PlanetKinds { get; init; } = [];

    /// <summary>Premade ship designs mods add, given to human players once they know every part.</summary>
    public IReadOnlyList<DesignTemplate> Designs { get; init; } = [];

    /// <summary>A tech's place among the techs of its kind, from 0: what the original reads as a mine field's or minesweeper's level, or an improvement's slot.</summary>
    public int IndexInKind(TechInfo tech) => OfKind(tech.Kind).TakeWhile(t => t.Id != tech.Id).Count();

    /// <summary>A tech by id (ids run from 1 in table order).</summary>
    public TechInfo Tech(int id)
    {
        return Techs[id - 1];
    }

    /// <summary>
    /// The techs of one kind in id order. The original walks from the
    /// first of the kind while the kind matches; techs of a kind are
    /// contiguous in its data, so taking every tech of the kind gives the
    /// same list, and also takes in mods' techs, which come after.
    /// </summary>
    public IEnumerable<TechInfo> OfKind(int kind)
    {
        return Techs.Where(t => t.Kind == kind);
    }
}
