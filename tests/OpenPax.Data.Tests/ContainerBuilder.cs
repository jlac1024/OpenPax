using System.Buffers.Binary;
using OpenPax.Data.Resources;

namespace OpenPax.Data.Tests;

/// <summary>Builds small synthetic resource containers for tests.</summary>
internal sealed class ContainerBuilder
{
    private readonly List<(ResourceType Type, List<(int Id, byte[] Body)> Items)> _types = [];

    public ContainerBuilder Add(string type, int id, params byte[] body)
    {
        var t = ResourceType.FromName(type);
        var group = _types.FirstOrDefault(g => g.Type == t);
        if (group.Items is null)
        {
            group = (t, []);
            _types.Add(group);
        }

        group.Items.Add((id, body));
        return this;
    }

    public byte[] Build()
    {
        var directory = 6 + _types.Sum(g => 8 + 8 * g.Items.Count);
        var output = new List<byte>();
        output.AddRange("II"u8.ToArray());
        output.AddRange(U32((uint)_types.Count));
        var offset = directory;
        var bodies = new List<byte>();
        foreach (var (type, items) in _types)
        {
            output.AddRange(U32(type.Value));
            output.AddRange(U32((uint)items.Count));
            foreach (var (id, body) in items)
            {
                output.AddRange(U32((uint)id));
                output.AddRange(U32((uint)offset));
                offset += body.Length;
                bodies.AddRange(body);
            }
        }

        output.AddRange(bodies);
        return [.. output];
    }

    private static byte[] U32(uint v)
    {
        var b = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        return b;
    }
}
