using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using OpenPax.Data.Toml;

namespace OpenPax.Data.Tables;

/// <summary>
/// The sine and cosine of every whole degree that fleet movement uses
/// (docs/sim/movement.md). They are six-decimal values typed into the
/// original program, many a step off true rounding, so they are copied
/// from the executable at import rather than computed.
/// </summary>
public sealed record DirectionTables(IReadOnlyList<double> Sin, IReadOnlyList<double> Cos)
{
    public const int Degrees = 360;
    public const string FileName = "directions.toml";

    // Where the original keeps them (virtual addresses).
    private const uint SinAddress = 0x004CD4A0;
    private const uint CosAddress = 0x004CDFE0;

    /// <summary>Reads both tables from the GOG executable (already verified by hash).</summary>
    public static DirectionTables FromExecutable(byte[] exe)
    {
        return new DirectionTables(ReadDoubles(exe, SinAddress), ReadDoubles(exe, CosAddress));
    }

    public string ToToml()
    {
        var text = new StringBuilder("# Sine and cosine by whole degree, copied from the original executable.\n");
        text.Append("sin = [").Append(string.Join(", ", Sin.Select(Format))).Append("]\n");
        text.Append("cos = [").Append(string.Join(", ", Cos.Select(Format))).Append("]\n");
        return text.ToString();
    }

    public static DirectionTables FromToml(string text)
    {
        var doc = TomlReader.Parse(text);
        return new DirectionTables(Numbers(doc, "sin"), Numbers(doc, "cos"));
    }

    private static string Format(double v)
    {
        // Always with a decimal point, so TOML reads it as a float.
        var s = v.ToString("R", CultureInfo.InvariantCulture);
        return s.Contains('.', StringComparison.Ordinal) || s.Contains('E', StringComparison.Ordinal) ? s : s + ".0";
    }

    private static double[] Numbers(Dictionary<string, object> doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v is not List<object> list || list.Count != Degrees)
        {
            throw new FormatException(string.Format(Strings.TomlMissing, key));
        }

        return [.. list.Select(o => o is double d ? d : (long)o)];
    }

    private static double[] ReadDoubles(byte[] exe, uint address)
    {
        var offset = FileOffset(exe, address);
        var values = new double[Degrees];
        for (var i = 0; i < Degrees; i++)
        {
            values[i] = BinaryPrimitives.ReadDoubleLittleEndian(exe.AsSpan(offset + i * 8, 8));
        }

        return values;
    }

    /// <summary>Maps a virtual address to a file offset through the PE section table.</summary>
    private static int FileOffset(byte[] exe, uint address)
    {
        var pe = BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(0x3C));
        var sections = BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan(pe + 6));
        var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan(pe + 20));
        var imageBase = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(pe + 24 + 28));
        var table = pe + 24 + optionalSize;
        var rva = address - imageBase;
        for (var i = 0; i < sections; i++)
        {
            var header = exe.AsSpan(table + i * 40, 40);
            var virtualAddress = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
            var rawSize = BinaryPrimitives.ReadUInt32LittleEndian(header[16..]);
            var rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(header[20..]);
            if (rva >= virtualAddress && rva + Degrees * 8 <= virtualAddress + rawSize)
            {
                return checked((int)(rawOffset + rva - virtualAddress));
            }
        }

        throw new FormatException(Strings.BadExecutable);
    }
}
