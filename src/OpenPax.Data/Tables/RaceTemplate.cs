using System.Buffers.Binary;
using OpenPax.Data.Resources;
using OpenPax.Data.Text;

namespace OpenPax.Data.Tables;

/// <summary>
/// A species (<c>RACE</c> resource, class 7105, in <c>Data/GameData</c>; also
/// the records of <c>Data/SpecData</c>). Values are as the original holds
/// them after loading: out-of-range values are dropped by its setters, and
/// a species over the point budget is reset to the defaults.
/// See docs/formats/tables.md for meanings.
/// </summary>
public sealed record RaceTemplate(
    int RaceId,
    string Name,
    string Description,
    int Picture,
    int PreferredTemperature,
    int TemperatureTolerance,
    IReadOnlyList<bool> Atmospheres,
    IReadOnlyList<bool> PhysicalTraits,
    IReadOnlyList<bool> CulturalTraits,
    IReadOnlyList<sbyte> RateModifiers,
    IReadOnlyList<sbyte> ResearchLevels)
{
    public const int ClassId = 7105;

    /// <summary>The original resets a species whose points exceed this.</summary>
    public const int PointBudget = 10;

    /// <summary>String list and index of the default name ("Unnamed") and description ("No Description").</summary>
    public static (int List, int Index) DefaultNameString => (100, 3);

    public static (int List, int Index) DefaultDescriptionString => (85, 0);

    private static readonly int[] _temperatures = [160, 130, 100, 70, 40, 10];
    private static readonly int[] _tolerances = [20, 40, 60];

    // Physical trait costs in point-budget order: anaerobic, subterranean, psychic, predator, aquatic.
    private static readonly int[] _physicalCost = [5, 5, 3, 2, 2];

    /// <summary>Reads a <c>RACE</c> resource body (starting with its class id).</summary>
    public static RaceTemplate Parse(ReadOnlySpan<byte> body, string defaultName, string defaultDescription)
    {
        if (body.Length < 4 || BinaryPrimitives.ReadInt32LittleEndian(body) != ClassId)
        {
            throw new ResourceFormatException(Strings.BadRace);
        }

        var pos = 4;
        var race = Read(body, ref pos, defaultName, defaultDescription);
        if (pos != body.Length)
        {
            throw new ResourceFormatException(Strings.BadRace);
        }

        return race;
    }

    /// <summary>
    /// Reads one record without a class id, as stored in <c>Data/SpecData</c>
    /// after its <c>II</c> mark, and moves <paramref name="pos"/> past it.
    /// </summary>
    public static RaceTemplate Read(ReadOnlySpan<byte> data, ref int pos, string defaultName, string defaultDescription)
    {
        // A fresh template holds the defaults; each setter
        // ignores values outside its range, so those fields keep them.
        var d = Defaults(defaultName, defaultDescription);
        var raceId = InRange(ReadInt32(data, ref pos), 0, 999) ?? d.RaceId;
        var name = ReadString(data, ref pos);
        var description = ReadString(data, ref pos);
        var picture = InRange(ReadInt32(data, ref pos), 0, 17) ?? d.Picture;
        var temp = ReadInt32(data, ref pos);
        var temperature = _temperatures.Contains(temp) ? temp : d.PreferredTemperature;
        var tol = ReadInt32(data, ref pos);
        var tolerance = _tolerances.Contains(tol) ? tol : d.TemperatureTolerance;
        var atmospheres = ReadFlags(data, ref pos, d.Atmospheres);
        var physical = ReadFlags(data, ref pos, d.PhysicalTraits);
        var cultural = ReadFlags(data, ref pos, d.CulturalTraits);
        var rates = ReadSigned(data, ref pos, d.RateModifiers, 5);
        var research = ReadSigned(data, ref pos, d.ResearchLevels, 1);

        var race = new RaceTemplate(raceId, name, description, picture, temperature, tolerance, atmospheres, physical, cultural, rates, research);

        // Over budget: the original resets every field, id and strings
        // included, rather than refusing the record.
        return race.Points() > PointBudget ? d : race;
    }

    /// <summary>All records of <c>Data/SpecData</c>: an <c>II</c> mark, then records until the end.</summary>
    public static IReadOnlyList<RaceTemplate> ReadSpeciesFile(ReadOnlySpan<byte> file, string defaultName, string defaultDescription)
    {
        // A fresh install ships SpecData empty, with not even the mark.
        if (file.Length == 0)
        {
            return [];
        }

        if (file.Length < 2 || file[0] != (byte)'I' || file[1] != (byte)'I')
        {
            throw new ResourceFormatException(Strings.BadRace);
        }

        var list = new List<RaceTemplate>();
        var pos = 2;
        while (pos < file.Length)
        {
            list.Add(Read(file, ref pos, defaultName, defaultDescription));
        }

        return list;
    }

    /// <summary>Writes one record as <see cref="Read"/> reads it: 46 bytes plus the two texts.</summary>
    public void Write(Stream output)
    {
        using var w = new BinaryWriter(output, System.Text.Encoding.Latin1, leaveOpen: true);
        w.Write(RaceId);
        WriteString(w, Name);
        WriteString(w, Description);
        w.Write(Picture);
        w.Write(PreferredTemperature);
        w.Write(TemperatureTolerance);
        foreach (var flags in (IReadOnlyList<bool>[])[Atmospheres, PhysicalTraits, CulturalTraits])
        {
            foreach (var f in flags)
            {
                w.Write((byte)(f ? 1 : 0));
            }
        }

        foreach (var v in RateModifiers.Concat(ResearchLevels))
        {
            w.Write(v);
        }
    }

    /// <summary>A whole <c>SpecData</c> file: the <c>II</c> mark, then each record.</summary>
    public static byte[] WriteSpeciesFile(IEnumerable<RaceTemplate> species)
    {
        using var stream = new MemoryStream();
        stream.Write("II"u8);
        foreach (var s in species)
        {
            s.Write(stream);
        }

        return stream.ToArray();
    }

    private static void WriteString(BinaryWriter w, string s)
    {
        var bytes = s.Select(c => Windows1252.GetByte(c) ?? (byte)'?').ToArray();
        w.Write((short)bytes.Length);
        w.Write(bytes);
    }

    public static RaceTemplate Defaults(string defaultName, string defaultDescription)
    {
        return new RaceTemplate(
            0,
            defaultName,
            defaultDescription,
            0,
            160,
            60,
            [true, false, false, false],
            new bool[5],
            new bool[7],
            new sbyte[5],
            new sbyte[5]);
    }

    /// <summary>Points the species spends; the designer allows at most <see cref="PointBudget"/>.</summary>
    public int Points()
    {
        var total = 0;
        if (!PhysicalTraits[0])
        {
            // Anaerobic species breathe anything, so only non-anaerobic ones
            // pay for extra atmospheres. With none set this is -1, as in the original.
            total = Atmospheres.Count(a => a) - 1;
        }

        for (var i = 0; i < PhysicalTraits.Count; i++)
        {
            total += PhysicalTraits[i] ? _physicalCost[i] : 0;
        }

        total += 2 * CulturalTraits.Count(c => c);
        total += RateModifiers.Sum(r => r) + ResearchLevels.Sum(r => r);
        return total;
    }

    private static int? InRange(int value, int min, int max)
    {
        return value >= min && value <= max ? value : null;
    }

    private static bool[] ReadFlags(ReadOnlySpan<byte> data, ref int pos, IReadOnlyList<bool> defaults)
    {
        var flags = new bool[defaults.Count];
        for (var i = 0; i < flags.Length; i++)
        {
            var b = ReadByte(data, ref pos);
            flags[i] = b < 2 ? b == 1 : defaults[i];
        }

        return flags;
    }

    private static sbyte[] ReadSigned(ReadOnlySpan<byte> data, ref int pos, IReadOnlyList<sbyte> defaults, int limit)
    {
        var values = new sbyte[defaults.Count];
        for (var i = 0; i < values.Length; i++)
        {
            var v = (sbyte)ReadByte(data, ref pos);
            values[i] = v >= -limit && v <= limit ? v : defaults[i];
        }

        return values;
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, ref int pos)
    {
        if (data.Length - pos < 4)
        {
            throw new ResourceFormatException(Strings.BadRace);
        }

        var v = BinaryPrimitives.ReadInt32LittleEndian(data[pos..]);
        pos += 4;
        return v;
    }

    private static byte ReadByte(ReadOnlySpan<byte> data, ref int pos)
    {
        if (data.Length - pos < 1)
        {
            throw new ResourceFormatException(Strings.BadRace);
        }

        return data[pos++];
    }

    private static string ReadString(ReadOnlySpan<byte> data, ref int pos)
    {
        if (data.Length - pos < 2)
        {
            throw new ResourceFormatException(Strings.BadRace);
        }

        var length = BinaryPrimitives.ReadUInt16LittleEndian(data[pos..]);
        if (data.Length - pos - 2 < length)
        {
            throw new ResourceFormatException(Strings.BadRace);
        }

        var s = Windows1252.GetString(data.Slice(pos + 2, length));
        pos += 2 + length;
        return s;
    }
}
