namespace OpenPax.Sim;

/// <summary>What a notice to a human player is about; each matches a player notice the original posts (docs/ui/messages.md).</summary>
public enum NoticeKind
{
    ResearchCompleted,
    Breakthrough,
    ShipFinished,
    ColonyLost,
    WarChanged,
    FirstContact,
    PlayerEliminated,
    SpySucceeded,
    SpyFailed,
    SpyStopped,
    SpyDamage,
    TradeOffered,
    TradeFailed,
    TradeAccepted,
    TradeRejected,

    /// <summary>The port's own: a message from a mod (Game.Tell), its words in the notice's Text.</summary>
    ModMessage,
}
