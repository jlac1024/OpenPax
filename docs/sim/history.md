# Empire history

Every 200 ticks (the scheduler's task, `time.md`) every player,
eliminated ones too, records ten statistics, keeping the last 50 samples
of each (10000 ticks) for the comparison graphs. Nothing in play reads
them; only the graph window and saved games do. No draws.

| Statistic | Value |
|---|---|
| Money | treasury, as a whole number |
| Colonies | number of colonies |
| Fleet strength | sum of fleet strengths |
| Population | sum of each colony's population, each as a whole number |
| Research | research points of the period times 10 |
| Construction | sum of each colony's construction |
| Yard work | sum over colonies of the first shipyard's work on its construction times 10 |
| Espionage | espionage points of the period times 10 |
| Techs known | techs learned (a tech learned twice counts twice) |
| Score | money / 20000 + colonies + population / 5 + research / 100 + fleet strength / 100 + construction / 100 + yard work / 100 + espionage / 100 + techs / 5, each part a whole number |

Each series starts empty (-1). A sample goes into the first empty slot,
or, once all 50 are used, the oldest is dropped. Quirk kept: a genuine -1
would count as empty (values are never negative in play).
