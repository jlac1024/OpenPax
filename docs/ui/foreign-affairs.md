# Foreign Affairs window

Mode 18, VIEW 15000, palette 5 (ctor, handler),
reached by F6, the bridge's Foreign Affairs station (22003) and the
notices about war, first contact, eliminations, espionage and trade.
Code: `src/OpenPax.Ui/Screens/ForeignAffairsScreen.cs`, the popups
`OfferPopup.cs`, `MissionPopup.cs` and `TradePopup.cs`; the treaty rules
in `src/OpenPax.Sim/Game.ForeignOrders.cs`.

## What it shows

- A tab for every other player in index order, eliminated ones crossed
  out, with a mark for the relation (war, neutral levels, allied). The
  picked player is remembered (player +0x540); nothing sets it before the
  first visit, so the port then picks the first tab.
- The picked player's name and species, its standing (No Contact, War,
  Neutral or Allied, in that color) and, once met, its planets and ships
  counted exactly (no sight test), and the research and trade it gave the
  player this year.
- Seven treaty boxes toward the picked player, each off, pending or in
  force.
- Offers waiting for the player: trade packages oldest first, then treaty
  offers. VIEW opens the offer popup.
- The player's spy missions, oldest first, each with the share paid so far.
  ABORT asks and drops the mission; what was paid is lost.
- The player's espionage points a year and its counter-ops, doubled for a
  psychic species.

## Treaty boxes

A box can be clicked only with contact (some treaty in force; first
contact makes the two players neutral) and when its rule allows:

| Box | Clickable | Click |
|---|---|---|
| War | not at war | war at once, both sides, ending every other state |
| Neutral | not neutral | from war: offer peace; from an alliance: neutral at once, which ends it; offered: withdraw |
| Trade Routes, Repair & Resupply | neutral | offer; in force or offered: cancel or withdraw |
| Allied | neutral with both of the above, not allied | offer, or withdraw |
| Joint Research, Share Information | allied | offer; in force or offered: cancel or withdraw |

A box with an offer to the player does nothing; offers are answered
through VIEW. Quirk kept: a click previews "pending" even for War. Once
allied, Trade Routes and Repair stay in force but cannot be canceled until
Neutral is taken again, the only way out of an alliance. Computer players
answer offers in their treaties module.

## Popups

- **Offer** (0x1F, VIEW 43000): a treaty, or a package listed as what is
  offered and what is asked under "X Offers:", "X Requests:" (or Gives,
  Demands). ACCEPT, REJECT or CANCEL. Nothing checks that the offer still
  stands. The original leaves two fields unset when it shows a package,
  which in network play could answer a treaty with stray values; only a
  human can propose a package, so single player never reaches it, and the
  port keeps those fields empty.
- **New mission** (0x1C, VIEW 40000), from STEAL, SUBVERT, SABOTAGE and
  BLACK OPS: the category's missions (steal ships, techs or star maps;
  turn an advisor or a governor; sabotage ships or a planet; rebellion or
  assassination), the level 1X, 2X or 4X, what it does, and its cost and
  chance. RUN starts it; points are paid over time, so nothing is checked.
  Quirk kept: "Take control of 1 world" overstates what the rebellion
  mission does.
- **Trade** (0x1D, VIEW 41000): what the player gives and what it asks
  for. ADD opens a picker: ships, colonies (all of the other player's,
  seen or not), techs the other side lacks, the star map, or credits in
  steps of 1000. SEND offers the package as it is, with no check.
- **Compare Empires** (0x1E, VIEW 42000): the empire history graph. A
  toggle per player in index order (its flag, crossed once out, in an
  outline of its line color), all on at first; the arrows step through the
  ten statistics (treasury, planets, ships, population, research,
  construction, ship points, espionage, techs, overall), wrapping round.
  Each shown player's 50 samples are drawn as a line, scaled so the highest
  sample among the shown players reaches the top; a line stops before its
  first missing sample. Pointing at a toggle names the player in its
  relation color. Code: `ComparePopup.cs`, `Widgets/HistoryGraphPane.cs`,
  `Widgets/EmpireTogglePane.cs`.
- **Governor picker** (0x1B) from REPLACE, for the foreign advisor (role 1).
- **Confirm** (0x1A) for ABORT.

## How it follows the game

The original listens to the player's broadcasts (treaty written, package
list changed, missions added or removed, the economy pass). The port
compares the treaty slots, packages and missions each frame and the
figures once a period.

## Not ported yet

SEND MESSAGE (popup 0x18, network chat), which belongs with networking.
