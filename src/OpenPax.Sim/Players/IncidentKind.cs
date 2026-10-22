namespace OpenPax.Sim.Players;

/// <summary>The kinds of event that move attitude scores (docs/sim/diplomacy.md).</summary>
public enum IncidentKind
{
    TradeRoutes = 0,
    JointResearch = 1,
    Disposition = 2,
    Stronger = 3,
    EnemyAttacked = 4,
    FriendAttacked = 5,
    SpyCaught = 6,
    ColonyTaken = 7,
    ShipDestroyed = 8,
    StructureDestroyed = 9,
    PlanetAttacked = 10,
    FleetAttacked = 11,
    ShipsGiven = 12,
    ShipsReceived = 13,
    PlanetsGiven = 14,
    PlanetsReceived = 15,
    TechsGiven = 16,
    TechsReceived = 17,
    CreditsGiven = 18,
    CreditsReceived = 19,
    MapGiven = 20,
    MapReceived = 21,
    TradeRejected = 22,
}
