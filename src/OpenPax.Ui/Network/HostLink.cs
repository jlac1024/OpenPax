using OpenPax.Net.Server;
using OpenPax.Sim;
using OpenPax.Sim.Commands;

namespace OpenPax.Ui.Network;

/// <summary>The hosting player's game: the server runs here, on the session's game and clock, and the host plays on that game directly.</summary>
public sealed class HostLink(GameServer server) : IGameLink
{
    public GameServer Server { get; } = server;

    public StepEvents? Tick(GameSession session, long now) =>
        Server.Tick(now) is { } stepped ? stepped | session.Game.TakeEvents(session.Local) : null;

    public StepEvents Send(GameSession session, Command command) =>
        Server.ApplyLocal(command) | session.Game.TakeEvents(session.Local);

    public IReadOnlyList<GameNotice> TakeNotices(GameSession session) => session.Game.TakeNotices(session.Local);

    public void Say(string text) => Server.Say(text);

    public IReadOnlyList<Net.Messages.SessionNewsMessage> TakeNews() => Server.TakeNews();

    public bool IsHost => true;

    /// <summary>The host asked for the vote; its yes is taken as given.</summary>
    public void VotePause(bool yes)
    {
    }

    public IReadOnlyList<(int From, string Text)> TakeChat() => [.. Server.TakeChat().Select(c => (c.From, c.Text))];
}
