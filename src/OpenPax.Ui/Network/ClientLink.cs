using OpenPax.Net.Client;
using OpenPax.Sim;
using OpenPax.Sim.Commands;

namespace OpenPax.Ui.Network;

/// <summary>A client's game: commands go to the server and the game changes only when its state arrives.</summary>
public sealed class ClientLink(GameClient client) : IGameLink
{
    public GameClient Client { get; } = client;

    public StepEvents? Tick(GameSession session, long now)
    {
        var changed = Client.Pump();
        var events = Client.TakeEvents();
        return changed || events != StepEvents.None ? events : null;
    }

    public StepEvents Send(GameSession session, Command command)
    {
        Client.Send(command);
        return StepEvents.None;
    }

    public IReadOnlyList<GameNotice> TakeNotices(GameSession session) => Client.TakeNotices();

    public void Say(string text) => Client.Say(text);

    public IReadOnlyList<Net.Messages.SessionNewsMessage> TakeNews() => Client.TakeNews();

    public bool IsHost => false;

    public void VotePause(bool yes) => Client.VotePause(yes);

    public IReadOnlyList<(int From, string Text)> TakeChat() => [.. Client.TakeChat().Select(c => (c.From, c.Text))];
}
