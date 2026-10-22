namespace OpenPax.Host.Headless;

/// <summary>A human player of a replayed game: name, flag, fleet color and stock species by its place in the game tables.</summary>
internal sealed record ReplayHuman(string Name, int Flag, int Color, int Species);
