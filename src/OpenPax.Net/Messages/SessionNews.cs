namespace OpenPax.Net.Messages;

/// <summary>What a <see cref="SessionNewsMessage"/> tells.</summary>
public enum SessionNews : byte
{
    SpeedChanged = 1,
    PlayerLeft = 2,

    /// <summary>The host asks to pause (speed 0); Player is the host. Every player answers with <see cref="PauseVoteCast"/>.</summary>
    PauseVoteOpened = 3,

    /// <summary>A player's answer, to the server: Value 1 yes, 0 no.</summary>
    PauseVoteCast = 4,

    /// <summary>The vote is over: Value 1 the game pauses, 0 it goes on (a no, a timeout).</summary>
    PauseVoteClosed = 5,
}
