using OpenPax.Sim.Commands;

namespace OpenPax.Sim;

/// <summary>
/// What a replay needs of a game being played: every command applied, with
/// how many steps had run before it (strategic ticks and battle steps
/// alike), and the state hash after every step.
/// Commands are kept as received, so ones that did nothing replay as nothing.
/// </summary>
public sealed class GameRecording
{
    public List<(long Step, Command Command)> Commands { get; } = [];

    public List<ulong> Hashes { get; } = [];
}
