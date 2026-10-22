using System.Buffers.Binary;
using OpenPax.Data.Resources;

namespace OpenPax.Data.Tables.Tech;

/// <summary>
/// A technology (<c>TECH</c> resource in Data/TechData). The class id picks
/// which fields follow the shared base; see docs/formats/tables.md.
/// </summary>
public sealed record TechRecord(int ClassId, TechBase Base, TechDetails Details)
{
    public static TechRecord Parse(ReadOnlySpan<byte> body)
    {
        if (body.Length < 4)
        {
            throw new ResourceFormatException(Strings.BadTech);
        }

        var classId = BinaryPrimitives.ReadInt32LittleEndian(body);
        var r = new RecordReader(body[4..]);
        var tech = TechBase.Read(ref r);
        TechDetails details = classId switch
        {
            6001 => new StarDrive(r.Int32(), r.Byte()),
            6002 => new TacticalDrive(r.Int32(), r.Int16()),
            6003 => new TargetingSensor(r.Int16(), r.Int16(), r.Int16()),
            6004 => new Ecm(r.Int32()),
            6005 => new ShipWeapon(r.Int32(), r.Int32(), [r.Int16(), r.Int16(), r.Int16(), r.Int16()], r.Byte(), r.Int32(), r.Int32(), r.Int32(), r.Byte(), r.Byte(), r.Byte(), r.Byte()),
            6006 => new Shield(r.Int32(), r.Int32(), r.Int32(), r.Byte(), r.Int32()),
            6007 => new Fighter(r.Int16(), r.Int16(), r.Int16(), r.Int16(), r.Int16(), r.Int16(), r.Int16()),
            6008 => new Armor(r.Byte(), r.Double()),
            6009 => new Installation(r.Int32(), r.Int32()),
            6010 => ReadImprovement(ref r),
            _ => throw new ResourceFormatException(Strings.BadTech),
        };

        if (!r.AtEnd)
        {
            throw new ResourceFormatException(Strings.BadTech);
        }

        return new TechRecord(classId, tech, details);
    }

    private static PlanetImprovement ReadImprovement(ref RecordReader r)
    {
        var perPop = new double[5];
        var flat = new double[5];
        for (var i = 0; i < 5; i++)
        {
            perPop[i] = r.Double();
            flat[i] = r.Double();
        }

        return new PlanetImprovement(perPop, flat);
    }
}
