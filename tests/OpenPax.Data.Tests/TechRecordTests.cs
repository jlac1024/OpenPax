using OpenPax.Data.Resources;
using OpenPax.Data.Tables.Tech;
using OpenPax.Data.Text;

namespace OpenPax.Data.Tests;

public class TechRecordTests
{
    [Fact]
    public void RefusesUnknownClassesAndLeftoverBytes()
    {
        var ecm = new byte[4 + 60 + 4];
        BitConverter.GetBytes(6004).CopyTo(ecm, 0);
        Assert.IsType<Ecm>(TechRecord.Parse(ecm).Details);

        BitConverter.GetBytes(6099).CopyTo(ecm, 0);
        Assert.Throws<ResourceFormatException>(() => TechRecord.Parse(ecm));

        BitConverter.GetBytes(6004).CopyTo(ecm, 0);
        Assert.Throws<ResourceFormatException>(() => TechRecord.Parse([.. ecm, 0]));
    }

    [Fact]
    public void EveryGameTechParsesExactly()
    {
        var techs = LoadAll(out _);

        Assert.Equal(239, techs.Count);
        var counts = techs.Values.GroupBy(t => t.ClassId).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(
            new Dictionary<int, int> { [6001] = 14, [6002] = 19, [6003] = 22, [6004] = 5, [6005] = 45, [6006] = 24, [6007] = 18, [6008] = 5, [6009] = 37, [6010] = 50 },
            counts);
        Assert.All(techs, kv => Assert.Equal(kv.Key, kv.Value.Base.TechId));
    }

    [Fact]
    public void KnownTechsDecodeAsTheirDescriptionsSay()
    {
        var techs = LoadAll(out var names);

        Assert.Equal("EW-01 Laser Cannon", names[98]);
        Assert.Equal([90, 0, 0, 0], ((ShipWeapon)techs[98].Details).DamageByRange.Select(d => (int)d));

        Assert.Equal("SS-15 Tensor Field", names[157]);
        Assert.Equal(new Shield(2, 2500, 40, 1, 15), techs[157].Details);

        Assert.Equal("Robotic Mining", names[200]);
        Assert.Equal(50.0, ((PlanetImprovement)techs[200].Details).Flat[PlanetImprovement.Construction]);
        Assert.Equal(TechKinds.PlanetaryImprovement, techs[200].Base.Kind);

        Assert.Equal(1.8, ((Armor)techs[189].Details).StructureMultiplier);
    }

    private static Dictionary<int, TechRecord> LoadAll(out Dictionary<int, string> names)
    {
        var folder = GameData.RequireFolder();
        var file = ResourceFile.Load(Path.Combine(folder, "Data", "TechData"));
        var strings = ResourceFile.Load(Path.Combine(folder, "Data", "StrData"));
        var techs = new Dictionary<int, TechRecord>();
        names = [];
        foreach (var e in file.Entries(ResourceType.Tech))
        {
            var tech = TechRecord.Parse(file.Get(ResourceType.Tech, e.Id).Span);
            techs[e.Id] = tech;
            names[e.Id] = StringList.Parse(strings.Get(ResourceType.StringList, tech.Base.StringListId).Span)[0];
        }

        return techs;
    }
}
