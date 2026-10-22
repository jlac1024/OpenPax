using System.Buffers.Binary;
using OpenPax.Data.Resources;

namespace OpenPax.Data.Tables;

/// <summary>
/// A ship hull (<c>HULL</c> resource, class 7108, ids 2000 + hull type).
/// A ship design picks a hull type 0 to 5 and looks up 2000 + type
/// .
/// </summary>
/// <param name="Structure">Scaled by a per-design factor to give the design's structure points.</param>
/// <param name="Capacity">Space for components; a design fits while its used space is at most this.</param>
/// <param name="Cost">Scaled by the same factor, plus component costs, gives the design's cost.</param>
public sealed record Hull(int Id, int Structure, int Capacity, int Cost)
{
    public const int ClassId = 7108;
    public const int FirstId = 2000;

    public static Hull Parse(int id, ReadOnlySpan<byte> body)
    {
        if (body.Length < 16 || BinaryPrimitives.ReadInt32LittleEndian(body) != ClassId)
        {
            throw new ResourceFormatException(Strings.BadHull);
        }

        return new Hull(
            id,
            BinaryPrimitives.ReadInt32LittleEndian(body[4..]),
            BinaryPrimitives.ReadInt32LittleEndian(body[8..]),
            BinaryPrimitives.ReadInt32LittleEndian(body[12..]));
    }
}
