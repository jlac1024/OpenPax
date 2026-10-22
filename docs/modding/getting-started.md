# Getting started

This page builds **Bounty**, a small C# mod: every enemy ship a player
destroys pays credits, sometimes double, and the player is told. The
finished mod ships with the game in `mods/bounty/`, and its source is in
the repository at `samples/Bounty/`.

## What you need

- The .NET 10 SDK.
- The game, built or unpacked, so you have its folder (the one holding
  `openpax` and `OpenPax.Modding.dll`).

## 1. Make the project

Make a folder for the mod's source and a project file in it. The game's
assemblies are referenced from the game's folder and not copied: the game
supplies them when it loads the mod.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="OpenPax.Modding" HintPath="/path/to/game/OpenPax.Modding.dll" Private="false" />
    <Reference Include="OpenPax.Sim" HintPath="/path/to/game/OpenPax.Sim.dll" Private="false" />
    <Reference Include="OpenPax.Data" HintPath="/path/to/game/OpenPax.Data.dll" Private="false" />
  </ItemGroup>
  <ItemGroup>
    <None Include="mod.toml" CopyToOutputDirectory="PreserveNewest" />
    <None Include="settings.toml" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

Inside the repository the samples use project references instead; see
`samples/Bounty/Bounty.csproj`.

## 2. Write the manifest

`mod.toml` names the mod and its assembly ([the manifest](manifest.md)
tells every field):

```toml
id = "bounty"
name = "Bounty"
version = "1.0"
description = "Credits for every enemy ship your fleets destroy, now and then a bonus."
assembly = "Bounty.dll"
```

## 3. Give it settings

A mod reads its own files from its folder. Bounty keeps its numbers in
`settings.toml`, so a player can change them without building anything:

```toml
credits_per_hull = 50
double_chance = 0.1
```

## 4. Write the mod

A mod is a public class implementing `IMod`. The game makes one when it
starts and calls `Load` once; the mod says there what it does, through the
`ModContext` it is given.

```csharp
using System.Globalization;
using OpenPax.Modding;
using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace Bounty;

public sealed class BountyMod : IMod
{
    private double _perHull = 50;
    private double _doubleChance = 0.1;
    private ModContext? _context;

    public void Load(ModContext context)
    {
        _context = context;
        var settings = context.ReadToml("settings.toml");
        _perHull = Number(settings, "credits_per_hull", _perHull);
        _doubleChance = Number(settings, "double_chance", _doubleChance);
        context.OnShipDestroyed(Paid);
    }

    private void Paid(Game game, Design ship, Player owner, Player? by)
    {
        if (by is null || by == owner || _context is not { } context)
        {
            return;
        }

        var bonus = context.Random(game).Chance(_doubleChance);
        var credits = _perHull * (ship.Type + 1) * (bonus ? 2 : 1);
        Game.GiveMoney(by, credits);

        var store = context.Store(game);
        var kills = store.GetInt("kills." + by.Index) + 1;
        store.SetInt("kills." + by.Index, kills);
        game.Tell(by, string.Format(CultureInfo.InvariantCulture,
            bonus ? "Bounty: {0} credits, doubled! ({1} kills)" : "Bounty: {0} credits ({1} kills)",
            credits, kills));
    }

    private static double Number(IReadOnlyDictionary<string, object> table, string key, double fallback) =>
        table.TryGetValue(key, out var v) ? v switch { long l => l, double d => d, _ => fallback } : fallback;
}
```

What each piece does:

- `context.ReadToml` reads a TOML file from the mod's folder, once, at load.
- `context.OnShipDestroyed` hears every ship destroyed, for every player,
  with the player that destroyed it when there is one. The other events
  are in [the reference](api.md#type-modcontext).
- `context.Random(game)` is the mod's own random number stream. It is the
  same in every copy of the game and saved with it, so a network game
  stays one game. Never use `System.Random` ([the rules](rules.md)).
- `Game.GiveMoney` and `game.Tell` are two of the actions a mod may take;
  `Tell` puts a line in the player's status bar and history.
- `context.Store(game)` keeps the mod's own values in the game, saved and
  sent to network clients with it.

## 5. Build and install

```bash
dotnet build -c Release
```

Copy `Bounty.dll`, `mod.toml` and `settings.toml` from the build output
into a folder named `bounty` in the `mods` folder of your user folder
([where mods are found](manifest.md#where-mods-are-found)).

## 6. Turn it on

Start the game, open Options, MODS, select Bounty and press ON, then
restart the game. Start a new game: the mod is part of it from then on,
and a saved game keeps it.

If something is wrong (a missing file, an assembly that does not load,
an exception in `Load`), the game leaves the mod out and says why in the
status line when it starts. Lines a mod writes with `context.Log` show
there too.

## Next

- [Rules for mod code](rules.md) before writing rules of your own.
- [The API reference](api.md) for everything a mod can reach.
- [Simple Combat](samples.md) for a larger mod: it replaces battles.
