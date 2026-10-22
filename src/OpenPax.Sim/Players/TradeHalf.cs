namespace OpenPax.Sim.Players;

/// <summary>What one side of a trade package hands over (docs/sim/trade.md).</summary>
public sealed class TradeHalf
{
    /// <summary>Object ids of planets with the side's colonies (+0xC).</summary>
    public List<int> Planets { get; } = [];

    /// <summary>Tech ids (+0x18).</summary>
    public List<int> Techs { get; } = [];

    /// <summary>Ship ids (+0x24).</summary>
    public List<int> Ships { get; } = [];

    /// <summary>The side's star map: every system it has seen (+0x30).</summary>
    public bool Map { get; set; }

    /// <summary>Credits, a whole number (+0x34).</summary>
    public int Credits { get; set; }

    /// <summary>Anything at all in this half.</summary>
    public bool HasAnything => Planets.Count > 0 || Techs.Count > 0 || Ships.Count > 0 || Credits > 0 || Map;

    internal TradeHalf Copy()
    {
        var copy = new TradeHalf { Map = Map, Credits = Credits };
        copy.Planets.AddRange(Planets);
        copy.Techs.AddRange(Techs);
        copy.Ships.AddRange(Ships);
        return copy;
    }

    internal bool SameItems(TradeHalf other)
    {
        return Planets.SequenceEqual(other.Planets) && Techs.SequenceEqual(other.Techs) && Ships.SequenceEqual(other.Ships);
    }

    public void AddTo(StateHash hash)
    {
        foreach (var list in new[] { Planets, Techs, Ships })
        {
            hash.Add(list.Count);
            foreach (var id in list)
            {
                hash.Add(id);
            }
        }

        hash.Add(Map ? 1 : 0).Add(Credits);
    }
}
