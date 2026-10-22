using System.Buffers.Binary;
using OpenPax.Data.Resources;
using OpenPax.Data.Tables;
using OpenPax.Data.Text;

namespace OpenPax.Data.Tests;

public class RaceTemplateTests
{
    private const string Unnamed = "Unnamed";
    private const string NoDescription = "No Description";

    [Fact]
    public void ReadsAllFields()
    {
        var race = RaceTemplate.Parse(Body(Record(id: 3, name: "Gorak", temp: 160, tol: 60, atm: [0, 1, 0, 1], phys: [0, 0, 0, 1, 1], cult: [1, 0, 0, 0, 0, 0, 0], rates: [0, 1, 1, 0, 0], research: [1, 0, 0, 0, 0])), Unnamed, NoDescription);

        Assert.Equal(3, race.RaceId);
        Assert.Equal("Gorak", race.Name);
        Assert.Equal(160, race.PreferredTemperature);
        Assert.Equal([false, true, false, true], race.Atmospheres);
        Assert.Equal([false, false, false, true, true], race.PhysicalTraits);
        Assert.Equal([1, 0, 0, 0, 0], race.ResearchLevels.Select(r => (int)r));
        Assert.Equal(10, race.Points());
    }

    [Fact]
    public void OutOfRangeValuesKeepTheDefaults()
    {
        // 55 is not an allowed temperature, 7 not a flag, 9 past the rate range.
        var race = RaceTemplate.Parse(Body(Record(id: 1, name: "X", temp: 55, tol: 60, atm: [7, 1, 0, 0], phys: [0, 0, 0, 0, 0], cult: [0, 0, 0, 0, 0, 0, 0], rates: [9, 0, 0, 0, 0], research: [0, 0, 0, 0, 0])), Unnamed, NoDescription);

        Assert.Equal(160, race.PreferredTemperature);
        Assert.True(race.Atmospheres[0]);
        Assert.Equal(0, race.RateModifiers[0]);
    }

    [Fact]
    public void OverBudgetResetsEverything()
    {
        // Anaerobic (5) + subterranean (5) + psychic (3) = 13 points.
        var race = RaceTemplate.Parse(Body(Record(id: 4, name: "Greedy", temp: 100, tol: 20, atm: [1, 0, 0, 0], phys: [1, 1, 1, 0, 0], cult: [0, 0, 0, 0, 0, 0, 0], rates: [0, 0, 0, 0, 0], research: [0, 0, 0, 0, 0])), Unnamed, NoDescription);

        Assert.Equal(0, race.RaceId);
        Assert.Equal(Unnamed, race.Name);
        Assert.Equal(NoDescription, race.Description);
        Assert.Equal(160, race.PreferredTemperature);
        Assert.Equal(60, race.TemperatureTolerance);
        Assert.False(race.PhysicalTraits[0]);
    }

    [Fact]
    public void SpeciesFileHoldsRecordsUntilTheEnd()
    {
        var one = Record(id: 9, name: "A", temp: 10, tol: 40, atm: [1, 0, 0, 0], phys: [0, 0, 0, 0, 0], cult: [0, 0, 0, 0, 0, 0, 0], rates: [0, 0, 0, 0, 0], research: [0, 0, 0, 0, 0]);
        byte[] file = [.. "II"u8, .. one, .. one];

        Assert.Equal(2, RaceTemplate.ReadSpeciesFile(file, Unnamed, NoDescription).Count);
        Assert.Empty(RaceTemplate.ReadSpeciesFile([], Unnamed, NoDescription));
    }

    [Fact]
    public void TheBuiltInSpeciesMatchTheOriginal()
    {
        var folder = GameData.RequireFolder();
        var strings = ResourceFile.Load(Path.Combine(folder, "Data", "StrData"));
        var unnamed = StringList.Parse(strings.Get(ResourceType.StringList, 100).Span)[3];
        var noDescription = StringList.Parse(strings.Get(ResourceType.StringList, 85).Span)[0];
        Assert.Equal(Unnamed, unnamed);
        Assert.Equal(NoDescription, noDescription);

        var file = ResourceFile.Load(Path.Combine(folder, "Data", "GameData"));
        var races = file.Entries(ResourceType.Race).Select(e => RaceTemplate.Parse(file.Get(ResourceType.Race, e.Id).Span, unnamed, noDescription)).ToArray();

        Assert.Equal(["Humans", "Kar'Tsoran", "Gorak", "Yssla", "Schreki", "Tekari", "D'naren", "Kybus"], races.Select(r => r.Name));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], races.Select(r => r.RaceId));
        Assert.Equal([100, 40, 160, 100, 160, 10, 100, 70], races.Select(r => r.PreferredTemperature));
        // Every built-in species spends exactly the full budget, so none is reset.
        Assert.All(races, r => Assert.Equal(RaceTemplate.PointBudget, r.Points()));
    }

    private static byte[] Body(byte[] record)
    {
        var body = new byte[4 + record.Length];
        BinaryPrimitives.WriteInt32LittleEndian(body, RaceTemplate.ClassId);
        record.CopyTo(body, 4);
        return body;
    }

    private static byte[] Record(int id, string name, int temp, int tol, byte[] atm, byte[] phys, byte[] cult, sbyte[] rates, sbyte[] research)
    {
        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(id));
        foreach (var s in new[] { name, "" })
        {
            bytes.AddRange(BitConverter.GetBytes((ushort)s.Length));
            bytes.AddRange(s.Select(c => (byte)c));
        }

        bytes.AddRange(BitConverter.GetBytes(0)); // picture
        bytes.AddRange(BitConverter.GetBytes(temp));
        bytes.AddRange(BitConverter.GetBytes(tol));
        bytes.AddRange(atm);
        bytes.AddRange(phys);
        bytes.AddRange(cult);
        bytes.AddRange(rates.Select(r => (byte)r));
        bytes.AddRange(research.Select(r => (byte)r));
        return [.. bytes];
    }

    [Fact]
    public void ASpeciesFileWrittenReadsBackTheSame()
    {
        var species = new RaceTemplate(3, "Glorbians", "Slow but sure.", 12, 40, 60,
            [false, true, false, true], [false, true, false, false, true], [true, false, false, false, false, false, true],
            [-2, 0, 1, 0, -1], [1, 0, -1, 0, 0]);
        var file = RaceTemplate.WriteSpeciesFile([species, species with { Name = "Twice" }]);
        Assert.Equal((byte)'I', file[0]);
        Assert.Equal(2 + 2 * 46 + "Glorbians".Length + "Twice".Length + 2 * "Slow but sure.".Length, file.Length);
        var back = RaceTemplate.ReadSpeciesFile(file, "Unnamed", "No Description");
        Assert.Equal(2, back.Count);
        Assert.Equal(species.Name, back[0].Name);
        Assert.Equal(species.Description, back[0].Description);
        Assert.Equal((species.RaceId, species.Picture, species.PreferredTemperature, species.TemperatureTolerance),
            (back[0].RaceId, back[0].Picture, back[0].PreferredTemperature, back[0].TemperatureTolerance));
        Assert.Equal(species.Atmospheres, back[0].Atmospheres);
        Assert.Equal(species.PhysicalTraits, back[0].PhysicalTraits);
        Assert.Equal(species.CulturalTraits, back[0].CulturalTraits);
        Assert.Equal(species.RateModifiers, back[0].RateModifiers);
        Assert.Equal(species.ResearchLevels, back[0].ResearchLevels);
        Assert.Equal("Twice", back[1].Name);
    }
}
