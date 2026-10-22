# Gameplay fix candidates

The port keeps the original's bugs that affect play (the project plan, decision
8), each marked "Bug kept" or "Quirk kept" where it is ported. Fixes come
as separate toggles, off by default unless Jeff decides otherwise
(decision 15). These are the ones that change how the game plays, as
opposed to the many display oddities (a "+-0.3" growth reading, which label
lights up), in plain terms.

## Things lost

1. **Trading over 21 ships** (Game.Trade.cs): a fleet holds 21 ships. Past
   21 another fleet is made but left empty, and the rest of the ships are
   lost.
2. **Ships given to or stolen by a computer player vanish** (Game.Trade.cs,
   Game.Espionage.cs): the computer player gets an empty fleet.
3. **Scrapping hits damaged units first** (Game.ColonyOrders.cs): scrapping
   a structure takes the first unit below full health, so a unit damaged in
   battle goes before the one meant; the work spent on it is lost.
4. **Shipyard overflow** (Game.Shipyard.cs): the work left over when a ship
   finishes is passed on in the yard's progress units rather than as
   construction, so the next ship gains or loses a little.

## Combat

5. **Fleets left out of battles** (Game.Combat.cs): a fleet close enough to
   join a fleet battle is left out when the slot it would come in from is
   free, which is the usual case.
6. **The wrong relation in planet battles** (Game.Combat.cs): which nearby
   fleets join the defense reads the defender's relation twice instead of
   the attacker's, so some fleets join the wrong side or none.
7. **Fast shots pass through planets** (Game.CombatWeapons.cs): a planet's
   blocks are small, so a fast shot can step over one and fly on.
8. **Shots aim past the target** (Game.CombatWeapons.cs): the lead is two
   steps past where shot and target meet, so moving targets are missed
   more.
9. **Experience twice** (Game.CombatResolve.cs): a battle's experience is
   given once in the last round and once more after it.

## Diplomacy and orders

10. **Strangers take offense** (Game.Diplomacy.cs): when an empire is
    attacked, empires it never met count as its friends, so they think
    worse of the attacker, though they know nothing of the victim.
11. **Refits at a foreign yard never start** (Game.ShipOrders.cs): a refit
    ordered at an ally's shipyard is accepted but never happens.
12. **The colonize key needs no colony ship** (Game.Commands.cs): "c"
    founds a colony from any fleet, even one with no colony ship left or
    no longer at the planet.

## Picked (Jeff, 2026-10-22)

All twelve are fixes now, off by default (1 and 10 added after a first
pick of ten, whose wording had them backwards): Options, GAMEPLAY tab. Each is a `Sim.Fixes`
flag; the game carries its set (`Game.Fixes`), taken from the settings
when a new game starts (the host's for a network game, which guests get
with its state) and kept in saves. A loaded game keeps the set it was
started with. Only a game with fixes puts them into its state hash, so
games as the original keep their hashes and replays.

| Fix | Setting | What changes |
|---|---|---|
| 2 | FixComputerKeepsShips | traded and stolen ships reach a computer player |
| 3 | FixScrapUnderWork | Stop Work takes a unit only from a stack still being built |
| 4 | FixShipyardOverflow | the leftover is the unused share of the construction put in |
| 5 | FixFleetsJoinBattles | a nearby fleet whose own slot is free comes in there |
| 6 | FixPlanetBattleSides | a fleet on terms with the attacker attacks, one on terms with the defender defends |
| 7 | FixShotsHitPlanets | a shot whose next step passes within a block's radius hits it |
| 8 | FixShotsLeadTrue | the aim is the step found closest, not two past it |
| 9 | FixExperienceOnce | experience is given once per battle |
| 11 | FixOwnYardRefits | a refit is refused away from the player's own yards |
| 12 | FixColonizeNeedsShip | the colonize key needs the fleet at the planet with a colony ship |
| 1 | FixTradeKeepsAllShips | a full fleet hands the rest of a trade's ships to a new one |
| 10 | FixStrangersIndifferent | an empire that never met the victim does not think worse of the attacker |
