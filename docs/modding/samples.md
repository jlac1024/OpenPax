# The samples

Four mods ship with the game in `mods/`, off until turned on. Their
source is in the repository under `samples/`.

## Bounty

A small C# mod, built step by step in [getting started](getting-started.md):
credits for every enemy ship destroyed, by its hull, sometimes double, and
a message for the player who earned them. Its numbers are in
`settings.toml`.

## Arsenal

A content pack, no code ([content](content.md)): `data/content/arsenal.toml`
adds a beam weapon past the game's best, a stronger shield, an orbital
farm improvement (a 51st improvement; colonies make room for it), a
crystalline planet type nobody can breathe but rich in wealth, and a
premade cruiser, the Lancer, built from the new weapon and shield. Each
new tech copies one of the game's (`like`) and changes what differs.

## Night Menu

A data pack: the Game Start picture at night. Its picture is made at
install time from the player's own game data (`samples/NightMenu` reads
the imported picture, moves every color to the palette entry nearest a
darker, bluer one, and writes the pack), so no game art is in the
repository. See [data packs](data-packs.md).

## Simple Combat

A large C# mod that replaces battles. There is no battle screen, and the
game never stops for a battle.

- A fleet that attacks a fleet stays engaged with it.
- Fleets at war also engage on their own when they come within 50 of
  each other (the original's battle joining distance) or circle the same
  uncolonized planet.
- At a colony only an attack starts a fight, and it is a siege: the
  attacking fleet goes into orbit and besieges the planet.
- Either side can reinforce. Any enemy fleet in the planet's orbit joins
  the siege, and any fleet of the owner's or an ally's there joins the
  defense, including a computer player's new fleet sitting on its
  shipyard planet. Computer players do this on their own.
- The planet lists show each colony's rating, "+attack ATK +defense DEF",
  and with ATTACK waiting the pointer shows an attack's chance.

### A round

Every few steps a round is fought. The original's battle rules become
sums, their rolls replaced by expected values, so nothing is drawn from
the game's random numbers.

- Each side's firepower is its weapons' best range band by condition:
  beams (energy, projectile, area), missiles, drones (three craft a
  flight), point defense and bombs.
- Beams and drones hit at aim minus hull defense plus 50 in 100. Missiles
  hit at tracking minus ECM, less the target's fighters' missile defense,
  plus 100. Neither ever drops below 5 in 100. Orbitals aim at 100 and
  have no hull defense or ECM.
- Point defense in full, plus half of short range guns (reach 500 or
  less) and half of a side's own drones, stops incoming missiles and
  drones, shared between them, at most 80 percent. Drones dodge by their
  defense.
- Fire is shared over the ships it falls on, the bigger drawing more of
  it, so a fleet wears down together. Each ship's share meets its own
  shield, which cuts beams and drones by its percent and soaks up to its
  capacity, but never stops missiles. What a destroyed ship could not
  take goes on to the rest, the weakest first.
- Some of what lands goes into a ship's parts instead of its hull, as the
  original's hits do: the share is the original's chance of a hit landing
  on a component, which grows as the ship is battered. It is spread over
  the sensors, ECM, shield, weapons and specials by the room each takes.
  Damaged weapons fire weaker and a damaged shield stops less, so a
  battered fleet fights worse.
- A fleet joining a siege first flies in under the defenses' guns when
  they outrange its weapons: the reach they have past its own, over its
  slowest ship's battle speed, in rounds of fire it takes without
  answering (`approach_steps_per_round`, `most_approach_rounds`).
- At a besieged planet the fire falls on the defending fleets, then the
  orbitals (missile bases, battle stations, fighter bases), then the mine
  fields the besiegers can see. Only then does it fall on the people,
  where every round lands as in the original's bombing: bombs in full,
  other weapons in part.
- Mine fields strike the besiegers every round, through shields. A side
  with no minesweeper of the field's level or better cannot see the
  mines: every strike lands and the field cannot be fired on. With one,
  most strikes are swept aside, and the sweepers and point defense clear
  mines each round.
- Experience counts as in the original: a ship's level raises its aim and
  its hull defense. After each round every ship of a side that fought
  gains a point, plus its share of the kill credit (a tenth of each enemy
  ship's structure, and of each orbital unit's hit points). Training bases
  still train ships up to level 2 outside battle.
- The colony falls to the besiegers once it has no defenders and no
  orbitals left and its population is near zero. Mine fields nobody could
  see go with it.

### The attack's chance

It compares how long the attackers would last under the defenders' fire
(mines included) with how long the defenders would last, plus, at a
planet, the time to bring its people down. Even odds mean the two would
wear each other out together. The attacker's own and allied fleets
already circling the planet count with it.

### Its numbers

Every number is in `simple-combat.toml` in the mod's folder, each with a
note on what it does. Copy the mod to your user folder's `mods` folder to
change them; that copy then wins over the shipped one.

### The API it uses

`OnBattle` takes every battle over; `OnStep` fights the rounds;
`Game.ReportFights` puts the fights on the system view; `OnColonyRating`
and `OnAttackOdds` give the planet list and the pointer their numbers;
`DamageShip`, `DamageStructure`, `Bombard`, `CaptureColony` and
`DeclareWar` change the game. See [the reference](api.md).
