using OpenPax.Sim;
using OpenPax.Sim.Commands;

namespace OpenPax.Ui.Network;

/// <summary>
/// How a session reaches its game when it is not single player: as the
/// host running the server, or as a client of one.
/// </summary>
public interface IGameLink
{
    /// <summary>A frame: the game moves on or new state arrives. The local player's events, or null when nothing happened.</summary>
    StepEvents? Tick(GameSession session, long now);

    /// <summary>The local player's command, applied or sent; any events it raised at once.</summary>
    StepEvents Send(GameSession session, Command command);

    /// <summary>The local player's notices since the last call.</summary>
    IReadOnlyList<GameNotice> TakeNotices(GameSession session);

    /// <summary>A line of chat to everyone in the game.</summary>
    void Say(string text);

    /// <summary>Session news since the last call: speed changes (to a client) and players gone.</summary>
    IReadOnlyList<Net.Messages.SessionNewsMessage> TakeNews();

    /// <summary>This player's answer to the host's pause vote.</summary>
    void VotePause(bool yes);

    /// <summary>Whether this end runs the server, and so sets the speed.</summary>
    bool IsHost { get; }

    /// <summary>Chat lines since the last call: who said it (a player's index, -1 the server) and what.</summary>
    IReadOnlyList<(int From, string Text)> TakeChat();
}
