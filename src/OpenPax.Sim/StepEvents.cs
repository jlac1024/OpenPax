namespace OpenPax.Sim;

/// <summary>What a tick asks of the host, which owns files and screens.</summary>
[Flags]
public enum StepEvents
{
    None = 0,

    /// <summary>The autosave task ran: save to PaxAutoSaveGame if the AutoSaveGame preference is on.</summary>
    Autosave = 1,

    /// <summary>The game ended and the local player is still in: show PlayerVictory.</summary>
    Victory = 2,

    /// <summary>The local player lost, or the game ended without them: show DefeatMovie.</summary>
    Defeat = 4,

    /// <summary>Another player was eliminated: show OtherPlayerElim and a message.</summary>
    PlayerEliminated = 8,

    /// <summary>Someone offered the local player a treaty.</summary>
    TreatyOffered = 16,

    /// <summary>The local player is now at war, or no longer, with someone.</summary>
    WarDeclared = 32,

    /// <summary>New samples for the comparison graphs.</summary>
    HistoryRecorded = 64,

    /// <summary>An espionage mission of or against the local player ended, with a report for it.</summary>
    EspionageReport = 128,

    /// <summary>A trade package was offered to the local player.</summary>
    TradeOffered = 256,

    /// <summary>A trade package the local player offered was accepted, failed or was rejected.</summary>
    TradeAnswered = 512,
}
