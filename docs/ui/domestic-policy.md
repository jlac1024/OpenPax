# Domestic Policy window

Mode 16, VIEW 17000, palette 5 (ctor, handler),
reached by F5 and the bridge's Domestic Policy station (22004) only. Code:
`src/OpenPax.Ui/Screens/DomesticPolicyScreen.cs`, the planet column in
`Widgets/PlanetColumn.cs`; the money figures in
`src/OpenPax.Sim/Game.Ledger.cs`.

## The budget

Every figure is a year's money (ten economy periods), cut to whole credits
one by one, so the lines need not add up to the net to the last credit:

| Line | Formula |
|---|---|
| Imperial Treasury | the player's money |
| Annual Income | income x 10 less the upkeep of other structures, shipyards and kind 6 structures |
| Research, Espionage, Construction | spending percent x 0.01 x that resource's improvement upkeep |
| CP Subsidy Cost | funded construction points x 20 |
| Fleet Cost | ship upkeep |
| Net Income | income x 10 less the three lines (multiplied the other way round), the subsidy, and all structure and ship upkeep |

The three spinners set the research, espionage and construction spending
percents, 0 to 100 each and independent of each other (unlike the Research
window's shares). Each period the economy charges a tenth of the line and
scales the colonies' output by the percent; when money runs short it cuts
subsidies first and then the percents, and the spinners follow at the next
period.

## The planet browser

A horizontal list, five 88 px columns in view, scrolled by the bar below.
It holds, in galaxy order and then sorted, every planet but gas giants in
a star the player has seen that passes both filters: a habitability one
(Good, Fair, Bad, for the player's species) and a status one (Colonized:
the player's own colonies; Explored: planets with no colony known). A
foreign colony in view is never listed; one in a star out of view counts as
an empty planet.

A column shows the planet (turning when picked, with a battle mark when
fought over), name, habitability, Wealth Rating (the planet's score) and
population: for an empty planet what the player could grow there, for a
colony "now/max +growth" (quirk kept: a shrinking one reads "+-0.4"). A
colony in view adds its owner and flag, income less its upkeep, espionage
(which can be negative), research, construction, subsidy and plan, a
shipyard mark and a bar for its defense structures.

The captions beside the columns sort them: name, habitability, wealth and
population for all; the colony figures put the player's own colonies first
and largest first. Picking a column selects the planet and makes its star
current. The arrows on the Project caption step an own colony's plan
through the six stock plans (Quickest, Population, Construction, Research,
Espionage, Finance); a planet in battle keeps its plan.

The filters and the sort caption are remembered (player +0x4FC to +0x504).
Nothing in the original sets them before the first visit, so what a new
game starts with is not known; by Jeff's choice the port starts with every
filter on and the list sorted by name.

## Also

REPLACE opens the governor picker for the domestic advisor (role 2, whose
skill raises every colony's construction). A double click on a star in the
small map opens the Sector Display. The window has no keys of its own.

The original listens to every player's broadcasts; the port instead
updates the budget and spinners once a period and refills the columns when
a colony is founded or lost or a star's sight changes.

## Not ported yet

Stopping the map's animation while the message history is open.
