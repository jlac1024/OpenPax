using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// The Foreign Affairs window's treaty rules: which treaty boxes a player
/// may click toward another, and what a click does (docs/ui/foreign-affairs.md).
/// </summary>
public sealed partial class Game
{
    /// <summary>
    /// Whether a treaty state's box can be clicked: War and
    /// Neutral unless in force; Trade Routes and Repair under Neutral; Allied
    /// under Neutral with both of those, unless already allied; Joint Research
    /// and Share Information under an alliance.
    /// </summary>
    public static bool TreatyOpen(Player me, Player other, int state)
    {
        var t = me.Treaties[other.Index];
        return state switch
        {
            Treaty.War or Treaty.Neutral => !t.Has(state),
            Treaty.TradeRoutes or Treaty.Resupply => t.Has(Treaty.Neutral),
            Treaty.Allied => !t.Has(Treaty.Allied) && t.Has(Treaty.Neutral) && t.Has(Treaty.Resupply) && t.Has(Treaty.TradeRoutes),
            _ => t.Has(Treaty.Allied),
        };
    }

    /// <summary>
    /// A treaty box clicked ( for War, Neutral and Allied,
    /// for the rest). War is declared at once. Neutral is
    /// offered from war, taken at once out of an alliance (ending it), or
    /// withdrawn when offered. Allied is offered or withdrawn. The others are
    /// offered when off, and canceled or withdrawn otherwise; an offer to
    /// me is answered elsewhere.
    /// </summary>
    public StepEvents TreatyClick(Player me, Player other, int state)
    {
        var slot = me.Treaties[other.Index].Slot(state);
        switch (state)
        {
            case Treaty.War:
                return slot == Treaty.Off ? ReplyTreaty(me, other, Treaty.War, Treaty.InForce) : StepEvents.None;
            case Treaty.Neutral:
                if (slot == Treaty.OfferedByMe)
                {
                    return ReplyTreaty(me, other, Treaty.Neutral, Treaty.Off);
                }

                if (slot != Treaty.Off)
                {
                    return StepEvents.None;
                }

                var t = me.Treaties[other.Index];
                return t.Has(Treaty.War) ? OfferTreaty(me, other, Treaty.Neutral)
                    : t.Has(Treaty.Allied) ? ReplyTreaty(me, other, Treaty.Neutral, Treaty.InForce)
                    : StepEvents.None;
            case Treaty.Allied:
                return slot switch
                {
                    Treaty.Off => OfferTreaty(me, other, Treaty.Allied),
                    Treaty.OfferedByMe => ReplyTreaty(me, other, Treaty.Allied, Treaty.Off),
                    _ => StepEvents.None,
                };
            default:
                return slot switch
                {
                    Treaty.Off => OfferTreaty(me, other, state),
                    Treaty.InForce or Treaty.OfferedByMe => ReplyTreaty(me, other, state, Treaty.Off),
                    _ => StepEvents.None,
                };
        }
    }
}
