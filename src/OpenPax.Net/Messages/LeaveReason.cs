namespace OpenPax.Net.Messages;

/// <summary>Why a player is gone, as the original's player lost message (0x1FA) tells it; each has its own line in string list 86.</summary>
public enum LeaveReason
{
    /// <summary>Lost sync (86, 1). The port recovers a desync instead, so it does not send this.</summary>
    LostSync = 1,

    /// <summary>The connection broke (86, 2: timed out).</summary>
    TimedOut = 2,

    /// <summary>The player closed the connection (86, 3: left the game).</summary>
    Left = 3,
}
