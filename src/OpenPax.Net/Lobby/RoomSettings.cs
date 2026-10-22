namespace OpenPax.Net.Lobby;

/// <summary>The galaxy a room's host has set: stars, computer opponents and difficulty, as Game Setup offers them.</summary>
public sealed record RoomSettings(int Stars, int Opponents, int Difficulty)
{
    public static RoomSettings Default { get; } = new(30, 2, 3);
}
