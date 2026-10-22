using OpenPax.Data.Tables;

namespace OpenPax.Ui;

/// <summary>
/// What the new game screens settle (the setup record filled at Start,
/// ): the universe seed and size, opponents, difficulty, and
/// the human's name, flag, fleet color and species.
/// </summary>
public sealed record NewGameChoices(
    uint Seed,
    int Stars,
    int Opponents,
    int Difficulty,
    string Name,
    int Flag,
    int FleetColor,
    RaceTemplate Species);
