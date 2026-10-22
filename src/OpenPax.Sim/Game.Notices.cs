using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// Notices and events for human players, waiting for whoever shows that
/// player's screens to take them: the screens themselves in single player
/// and for a hosting player, the game server for the others.
/// </summary>
public sealed partial class Game
{
    private readonly List<GameNotice> _notices = [];
    private readonly StepEvents[] _events = new StepEvents[Player.MaxPlayers];

    /// <summary>The player's notices since the last call, oldest first.</summary>
    public IReadOnlyList<GameNotice> TakeNotices(Player recipient)
    {
        var notices = _notices.Where(n => n.Recipient == recipient).ToList();
        _notices.RemoveAll(n => n.Recipient == recipient);
        return notices;
    }

    /// <summary>The player's events since the last call.</summary>
    public StepEvents TakeEvents(Player player)
    {
        var events = _events[player.Index];
        _events[player.Index] = StepEvents.None;
        return events;
    }

    /// <summary>
    /// An event for a human player. The original raises these only for its
    /// own machine's player; here every human gets theirs.
    /// </summary>
    private void Raise(Player player, StepEvents events)
    {
        if (!player.IsAi)
        {
            _events[player.Index] |= events;
        }
    }

    /// <summary>The original posts only for the local player; computer players get nothing, so headless games keep none.</summary>
    private void Notify(GameNotice notice)
    {
        if (!notice.Recipient.IsAi)
        {
            _notices.Add(notice);
        }
    }
}
