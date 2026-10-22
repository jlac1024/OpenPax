using OpenPax.Data.Resources;

namespace OpenPax.Data.Tables.Tech;

/// <summary>
/// Fields every technology has (fifteen int32, read). Name
/// and description are not here: they are strings 0 and 1 of string list
/// 100000 + <see cref="TechId"/>.
/// </summary>
/// <param name="TechId">Equals the resource id.</param>
/// <param name="Picture">Picture id in Image/Tech.img; all theory techs use 25000.</param>
/// <param name="ResearchGroup">Line of the research tree; 0 for theories.</param>
/// <param name="ResearchCost">Scaled 75 to 125 percent by the species' research level for the field.</param>
/// <param name="Cost">Per-component cost, scaled by tech level and summed into a design's cost, which is also each ship's upkeep.</param>
/// <param name="BuildCost">Feeds design and construction cost.</param>
/// <param name="Kind">Slot or role; see <see cref="TechKinds"/>. -1 theory, -2 planetary improvement.</param>
/// <param name="Prerequisites">Four tech ids, 0 for none.</param>
/// <param name="HitPoints">Structure of the built item.</param>
/// <param name="Size">Space used in a design, scaled by tech level.</param>
/// <param name="Unused13">Always 0; read and written but never used.</param>
/// <param name="Unused14">Always 0; read and written but never used.</param>
public sealed record TechBase(
    int TechId,
    int Picture,
    int ResearchGroup,
    int ResearchCost,
    int Cost,
    int BuildCost,
    int Kind,
    IReadOnlyList<int> Prerequisites,
    int HitPoints,
    int Size,
    int Unused13,
    int Unused14)
{
    /// <summary>String list holding a tech's name (entry 0) and description (entry 1).</summary>
    public int StringListId => 100000 + TechId;

    internal static TechBase Read(ref RecordReader r)
    {
        var id = r.Int32();
        var picture = r.Int32();
        var group = r.Int32();
        var research = r.Int32();
        var rating = r.Int32();
        var build = r.Int32();
        var kind = r.Int32();
        int[] prereqs = [r.Int32(), r.Int32(), r.Int32(), r.Int32()];
        return new TechBase(id, picture, group, research, rating, build, kind, prereqs, r.Int32(), r.Int32(), r.Int32(), r.Int32());
    }
}
