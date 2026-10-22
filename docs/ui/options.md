# Options (mode 5, the port's own)

Game Start's main buttons gain a sixth, Options, third after Multiplayer
Game. It is a copy of Show Credits' button with its own id (31190) and
caption; the six are spread evenly over the span the original's five
take. The single player and multiplayer choices show in the original
places, with Options hidden.

The screen is a form (Forms/Markup/Options.xml): PLAYER on the left, and a DISPLAY and a GAMEPLAY tab on the right. Every change is kept at
once; the config file is written when the game closes.

| Setting | Kept as | Used by |
|---|---|---|
| Your name | `[player] name` in openpax.toml | lobbies; Game Setup's empire name starts on it |
| Species in lobby rooms | preference `PreferredSpecies` (stock list index) | a lobby room's seat starts on it |
| Flag (1 to 30) | preference `FlagID` (0 to 29), the original's PrefData key | Game Setup and a lobby room start on it |
| Fleet (1 to 16) | preference `FleetID` (0 to 15) | as the flag; each fleet's ships have their own look, two hulls shown |
| Fullscreen | preference `Fullscreen` | the window follows it each frame, as with Alt+Enter |
| Fixes (GAMEPLAY tab) | preferences `Fix...`, one per fix, all off by default | a new game plays with the fixes on (docs/sim/fix-candidates.md); a loaded game keeps its own |
| Speed 0 pauses the game | preference `SpeedZeroPauses` (port's own gameplay toggle, on by default, decisions.md) | speed 0 holds the game instead of the original's slowest speed; in a network game the host's speed 0 asks everyone to agree first |
| Wait for the display (vsync) | preference `VSync` (port's own, on by default) | frames wait for the display's refresh; off, the frame pacer alone holds them to the display's rate |
| Upscaled art | preference `UpscaledArt` | live |
| Play movies | preference `PlayMovies` | live |
| A sky made for each system | preference `MadeSkies` (port's own, on by default) | the Sector Display's system view: a generated sky per system (docs/ui/system-sky.md), or the game's seven nebula pictures |
| Star speed | preference `StarSpeed`, 0 to 100, default 50 | how fast they fly: 0 holds them still, 100 is twice the default |
| Stars fly by behind menus | preference `MovingStars` (port's own, on by default) | the forms' backdrop, and the bars beside the original screens in a window wider or taller than 4:3 (moving beside the menus, still beside the game): a slow warp starfield, or the game's still one |
| Text font | preference `TextFont` | live |
| Text outline | preference `TextOutline` | live |

Game Setup in the original starts on "Your Name", flag 0 and fleet 0; the
port starts on the player's Options instead (the original text stands
when no name is set).
