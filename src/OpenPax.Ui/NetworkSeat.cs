using OpenPax.Data.Tables;

namespace OpenPax.Ui;

/// <summary>A human in a network game: the name from the lobby, a flag and fleet color, and the species.</summary>
public sealed record NetworkSeat(string Name, int Flag, int FleetColor, RaceTemplate Species);
