namespace OpenPax.Sim.Commands;

/// <summary>
/// A player's action as data: what a screen asks of the game, carried out
/// by <see cref="Game.Apply"/> between ticks. Objects are named by id, so a
/// command can be saved, sent to a server or kept in a replay. A command
/// that names something gone or not the player's does nothing.
/// </summary>
public abstract record Command(int Player);
