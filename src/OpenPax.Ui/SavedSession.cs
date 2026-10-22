using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace OpenPax.Ui;

/// <summary>
/// What a saved game holds: the game, the star in view and the selection
/// (the original saves both with the universe), and what the screens
/// remember between visits (the original keeps that in the player block).
/// The clock is not saved; a loaded game gets a new one, as in the original.
/// </summary>
public sealed class SavedSession
{
    public required Game Game { get; init; }

    public int CurrentStar { get; init; }

    public object? Selection { get; init; }

    public int BuildSortColumn { get; init; }

    public int LastDesignSlot { get; init; }

    public int ResearchField { get; init; }

    public bool ResearchShowsKnown { get; init; }

    public int ResearchSortColumn { get; init; }

    public bool FleetShowsYards { get; init; }

    public int FleetSortColumn { get; init; }

    public int ShipSortColumn { get; init; }

    public int DesignSortColumn { get; init; }

    public int YardSortColumn { get; init; }

    public bool[] DomesticFilters { get; init; } = [];

    public int DomesticSortColumn { get; init; }

    public int? ForeignPlayer { get; init; }

    public Design? DesignInProgress { get; init; }

    public int DesignCategory { get; init; }

    public int DesignSlot { get; init; }

    public int DesignAvailableSort { get; init; }

    public int DesignFittedSort { get; init; }

    /// <summary>The port's own: whether the console's cheats were on, so a loaded game has them as it was left (off in older saves).</summary>
    public bool CheatsOn { get; init; }
}
