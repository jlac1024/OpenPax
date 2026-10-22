using OpenPax.Sim;
using OpenPax.Sim.Commands;
using OpenPax.Ui.Network;

namespace OpenPax.Ui.Tests;

/// <summary>A network link that only keeps what the screens send.</summary>
internal sealed class RecordingLink(List<Command> sent) : IGameLink
{
    public StepEvents? Tick(GameSession session, long now) => null;

    public StepEvents Send(GameSession session, Command command)
    {
        sent.Add(command);
        return StepEvents.None;
    }

    public IReadOnlyList<GameNotice> TakeNotices(GameSession session) => [];

    public List<string> Said { get; } = [];

    public void Say(string text) => Said.Add(text);

    public IReadOnlyList<(int From, string Text)> TakeChat() => [];

    public IReadOnlyList<OpenPax.Net.Messages.SessionNewsMessage> TakeNews() => [];

    public bool IsHost => true;

    public void VotePause(bool yes)
    {
    }
}
