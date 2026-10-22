# Developer window (mode 21, VIEW 27000)

The developers' cheat window (ctor, handler),
palette 5, background Main 27000. It cannot be reached in the shipped
game: no code switches to mode 21, and the key meant for it, Ctrl Shift
Alt F12 (command 309 in the PaxIIAccelerators table), has no case in the
app's command handler. It opens with the "What?" voice
(37700) and asks for the game's music.

A list (27001, with scroll bar 27002) of the players, and eleven buttons
on the right. Each sends the cheat command (executed by
) for the player picked in the list:

| Button | Label (STR#) | Kind |
|---|---|---|
| 27003 | Give Lots $$ (20000/19) | 0 |
| 27004 | Build All (20000/20) | 1 |
| 27005 | Kill Player (20000/21) | 2 |
| 27006 | Research All (20000/22) | 3 |
| 27007 | Espionage Succeeded (20000/23) | 4 |
| 27008 | More Ships (20000/24) | 5 |
| 27009 | Colonize Sectors (20000/25) | 6 |
| 27010 | Explore All (20000/26) | 7 |
| 27011 | Planet Combat (20000/37) | 8 |
| 27012 | Fleet Combat (20000/38) | 9 |
| 27013 | Special Test (70/4) | 10 |

Kind 7 is also what the end window's Observe Game sends.

## What each kind does

| Kind | Effect |
|---|---|
| 0 | Money set to 900,000,000. |
| 1 | Every colony gets population 100,000 and up to five of each structure kind (the best known), then everything under way finishes and fleets are paid for. |
| 2 | The player is eliminated through the usual elimination check. |
| 3 | The techs in the research slots are learned and the slots cleared, then every tech not known is given. |
| 4 | Every spy mission succeeds and is removed. The success and detection rolls are still drawn, so the RNG sequence does not shift. |
| 5 | A new fleet at the first colony with one ship of each design until the fleet is full. A computer player skips hull types 0 and 2. |
| 6 | In every star the player can see, the first free planet that is not Class 5 is colonized. |
| 7 | Every star is observed. |
| 8 | The first other live player gets kinds 3 and the finish of kind 1, plus a fighter base and a minefield at their first colony; the player gets a kind 5 fleet sent there. |
| 9 | Both players get a kind 5 fleet at each other's first colony; the player's is sent in. |
| 10 | Bumps a sync test counter and nothing else. |

## In the port

The window is ported (`DeveloperScreen`, `Game.ApplyCheat`) and reached
from the developer console, which the original does not have; see
[console.md](console.md).
