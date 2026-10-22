namespace OpenPax.Sim.Players;

/// <summary>
/// A trade offer from one player to another (0x68 bytes): what
/// the proposer gives and what it asks for. It waits on the target's
/// pending list until answered (docs/sim/trade.md).
/// </summary>
public sealed class TradePackage(Player proposer, Player target)
{
    /// <summary>Who made the offer (+8).</summary>
    public Player Proposer { get; } = proposer;

    /// <summary>Who it is made to (+0x38).</summary>
    public Player Target { get; } = target;

    /// <summary>What the proposer gives.</summary>
    public TradeHalf Offered { get; private init; } = new();

    /// <summary>What the proposer asks the target to give.</summary>
    public TradeHalf Asked { get; private init; } = new();

    /// <summary>Something offered and nothing asked, both players still in.</summary>
    public bool IsGift => !Proposer.Eliminated && !Target.Eliminated && Offered.HasAnything && !Asked.HasAnything;

    /// <summary>Nothing offered and something asked, both players still in.</summary>
    public bool IsDemand => !Proposer.Eliminated && !Target.Eliminated && !Offered.HasAnything && Asked.HasAnything;

    internal TradePackage Copy()
    {
        return new TradePackage(Proposer, Target) { Offered = Offered.Copy(), Asked = Asked.Copy() };
    }

    /// <summary>
    /// Whether two packages match when an answer looks for the pending one.
    /// Quirk kept: the players, credits and map flags only rule a match out
    /// when all six differ, so in practice only the item lists count.
    /// </summary>
    internal bool Matches(TradePackage other)
    {
        var allDiffer = Proposer != other.Proposer && Target != other.Target
            && Offered.Map != other.Offered.Map && Offered.Credits != other.Offered.Credits
            && Asked.Map != other.Asked.Map && Asked.Credits != other.Asked.Credits;
        return !allDiffer && Offered.SameItems(other.Offered) && Asked.SameItems(other.Asked);
    }

    public void AddTo(StateHash hash)
    {
        hash.Add(Proposer.Index).Add(Target.Index);
        Offered.AddTo(hash);
        Asked.AddTo(hash);
    }
}
