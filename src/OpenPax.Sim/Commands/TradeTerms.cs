using OpenPax.Sim.Players;

namespace OpenPax.Sim.Commands;

/// <summary>One half of a trade package as data: planet object ids, techs, ship ids, the star map and credits.</summary>
public sealed record TradeTerms(int[] Planets, int[] Techs, int[] Ships, bool Map, int Credits)
{
    public static TradeTerms Of(TradeHalf half) => new([.. half.Planets], [.. half.Techs], [.. half.Ships], half.Map, half.Credits);

    internal void CopyTo(TradeHalf half)
    {
        half.Planets.AddRange(Planets);
        half.Techs.AddRange(Techs);
        half.Ships.AddRange(Ships);
        half.Map = Map;
        half.Credits = Credits;
    }
}
