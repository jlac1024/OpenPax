using System.Text;

namespace OpenPax.Sim;

/// <summary>
/// A 64-bit FNV-1a hash over game state, fed field by field. Stable across
/// runs, processes and platforms (unlike System.HashCode, which is seeded
/// per process), so it can be compared between machines and replays.
/// </summary>
public sealed class StateHash
{
    private const ulong Prime = 0x100000001B3;
    private ulong _value = 0xCBF29CE484222325;

    public ulong Value => _value;

    public StateHash Add(int v)
    {
        return AddBytes((ulong)(uint)v, 4);
    }

    public StateHash Add(long v)
    {
        return AddBytes((ulong)v, 8);
    }

    public StateHash Add(double v)
    {
        // The exact bits, so any difference in the last place shows.
        return AddBytes((ulong)BitConverter.DoubleToInt64Bits(v), 8);
    }

    public StateHash Add(string v)
    {
        Add(v.Length);
        foreach (var b in Encoding.UTF8.GetBytes(v))
        {
            Mix(b);
        }

        return this;
    }

    private StateHash AddBytes(ulong v, int count)
    {
        for (var i = 0; i < count; i++)
        {
            Mix((byte)(v >> (8 * i)));
        }

        return this;
    }

    private void Mix(byte b)
    {
        _value = (_value ^ b) * Prime;
    }
}
