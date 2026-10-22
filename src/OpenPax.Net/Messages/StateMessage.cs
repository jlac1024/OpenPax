using OpenPax.Net.State;
using OpenPax.Sim;

namespace OpenPax.Net.Messages;

/// <summary>
/// The game as the server has it after a tick: the saved bytes, whole or as
/// a difference from the last state sent to this client (<see cref="StateDiff"/>),
/// with the state hash, and the events and notices for the client's player
/// since the last state.
/// </summary>
public sealed record StateMessage(long Tick, ulong Hash, bool Full, int Length, byte[] Payload, StepEvents Events, NoticeData[] Notices) : NetMessage;
