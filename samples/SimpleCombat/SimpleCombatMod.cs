using OpenPax.Modding;
using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace SimpleCombat;

/// <summary>
/// The Simple Combat mod (docs/modding.md): battles never open, and the game
/// never stops for one. A fleet that attacks a fleet is engaged with it, as
/// are fleets at war that come close away from any colony; a
/// fleet that attacks a colony goes into orbit and besieges it, and more
/// fleets join either side by flying into that orbit. Every few steps a
/// round is fought by the battle rules boiled down to sums (Fight,
/// Firepower): hit chances against hull defense and ECM, point defense and
/// short range guns against missiles and drones, shields against beams,
/// mines that only minesweepers can see. A besieged colony loses its
/// defending fleets, then its orbitals, then its people, and falls.
/// </summary>
public sealed class SimpleCombatMod : IMod
{
    // The engagements and sieges, as "attacker:target" object ids, in the game's mod state, so they outlast a save.
    private const string EngagedKey = "simple-combat.engaged";
    private const string SiegeKey = "simple-combat.sieges";

    // How near a planet an idle fleet that is not circling anything counts as at it, in map units.
    private const int NearPlanet = 30;

    private Settings _settings = Settings.Default;

    public void Load(ModContext context)
    {
        _settings = Settings.Read(Path.Combine(context.Mod.Folder, "simple-combat.toml"));
        context.OnBattle(TakeOver);
        context.OnStep(Round);
        context.OnColonyRating(Rate);
        context.OnAttackOdds(Odds);
    }

    private bool TakeOver(Game game, Fleet attacker, ISpaceObject target)
    {
        // A planet: the game puts the fleet in orbit of it, and it besieges it while it stays.
        var (key, id) = target switch
        {
            Fleet f => (EngagedKey, f.ObjectId),
            Planet p => (SiegeKey, p.ObjectId),
            _ => (null, 0),
        };
        if (key is null)
        {
            return false;
        }

        // An attack is an act of war: the original's battle makes the two at war at its first hit.
        game.DeclareWar(attacker.Owner, target is Fleet t ? t.Owner : ((Planet)target).Owner!); // a planet attacked has an owner
        var pairs = Pairs(game, key);
        var pair = (attacker.ObjectId, id);
        if (!pairs.Contains(pair))
        {
            pairs.Add(pair);
            Save(game, key, pairs);
            Report(game);
        }

        return true;
    }

    private void Round(Game game)
    {
        Encounters(game);
        if (game.Tick % _settings.RoundEvery != 0)
        {
            return;
        }

        FleetFights(game);
        Sieges(game);
        Report(game);
    }

    /// <summary>
    /// Fleets at war that come close engage on their own, every step: in open
    /// space within engage_distance of each other, or circling the same
    /// uncolonized planet. A fleet circling a colony is left out; there the
    /// siege rules hold, an attack starting a siege and the colony's own and
    /// allied fleets defending it.
    /// </summary>
    private void Encounters(Game game)
    {
        var engaged = Pairs(game, EngagedKey);
        var known = engaged.SelectMany(p => new[] { p, (p.Target, p.Attacker) }).ToHashSet();
        var added = false;
        foreach (var star in game.Universe.Stars)
        {
            // At a colony counts a computer player's fleet sitting on it too (At).
            var colonies = star.Planets.Where(p => p.Colony is not null).ToList();
            var free = star.Fleets
                .Where(f => f.Ships.Count > 0 && !f.InCombat && !colonies.Any(p => At(f, p)))
                .OrderBy(f => f.ObjectId)
                .ToList();
            for (var i = 0; i < free.Count; i++)
            {
                for (var j = i + 1; j < free.Count; j++)
                {
                    var (a, b) = (free[i], free[j]);
                    if (!Game.AtWar(a.Owner, b.Owner) || known.Contains((a.ObjectId, b.ObjectId)) || !Close(a, b))
                    {
                        continue;
                    }

                    engaged.Add((a.ObjectId, b.ObjectId));
                    known.Add((a.ObjectId, b.ObjectId));
                    known.Add((b.ObjectId, a.ObjectId));
                    added = true;
                }
            }
        }

        if (added)
        {
            Save(game, EngagedKey, engaged);
            Report(game);
        }
    }

    private bool Close(Fleet a, Fleet b) =>
        (a.OrbitCenter is Planet p && b.OrbitCenter == p)
        || (Math.Abs(a.X - b.X) <= _settings.EngageDistance && Math.Abs(a.Y - b.Y) <= _settings.EngageDistance
            && (((double)(a.X - b.X) * (a.X - b.X)) + ((double)(a.Y - b.Y) * (a.Y - b.Y))) <= _settings.EngageDistance * _settings.EngageDistance);

    /// <summary>Tells the game which fights are on, so the system view shows them.</summary>
    private static void Report(Game game)
    {
        var fleets = game.Universe.Stars.SelectMany(s => s.Fleets).ToDictionary(f => f.ObjectId);
        var planets = game.Universe.Stars.SelectMany(s => s.Planets).ToDictionary(p => p.ObjectId);
        var fights = new List<ModFight>();
        foreach (var (a, b) in Pairs(game, EngagedKey))
        {
            if (fleets.TryGetValue(a, out var attacker) && fleets.TryGetValue(b, out var target))
            {
                fights.Add(new ModFight(attacker, target));
            }
        }

        foreach (var id in Pairs(game, SiegeKey).Select(p => p.Target).Distinct().Order())
        {
            if (planets.TryGetValue(id, out var planet) && planet.Colony is { } colony)
            {
                var (besiegers, defenders) = Sides(game, planet, colony);
                fights.AddRange(besiegers.Concat(defenders).Select(f => new ModFight(f, planet)));
            }
        }

        game.ReportFights(fights);
    }

    private void FleetFights(Game game)
    {
        var fleets = game.Universe.Stars.SelectMany(s => s.Fleets).ToDictionary(f => f.ObjectId);

        // An engagement ends when either fleet is gone, the two are apart, or no longer at war.
        var engaged = Pairs(game, EngagedKey)
            .Where(p => fleets.TryGetValue(p.Attacker, out var a) && fleets.TryGetValue(p.Target, out var b)
                && a.Ships.Count > 0 && b.Ships.Count > 0 && a.Star == b.Star && Game.AtWar(a.Owner, b.Owner))
            .Distinct()
            .ToList();

        // Each fleet fires once a round, its fire shared out over the fleets
        // it is engaged with; all fire from strength before the round lands.
        var opponents = new SortedDictionary<int, List<Fleet>>();
        foreach (var (a, b) in engaged)
        {
            Opponents(a).Add(fleets[b]);
            Opponents(b).Add(fleets[a]);
        }

        var incoming = new SortedDictionary<int, (Volley Fire, Player By)>();
        foreach (var (id, against) in opponents)
        {
            var shooter = fleets[id];
            var fire = Firepower.Of(game, [shooter]);
            foreach (var target in against)
            {
                var guard = Firepower.Of(game, [target]);
                var v = Fight.Fire(fire, guard, guard.SmallCraftDefense(_settings), _settings).Times(1.0 / against.Count);
                incoming[target.ObjectId] = incoming.TryGetValue(target.ObjectId, out var had) ? (had.Fire + v, had.By) : (v, shooter.Owner);
            }
        }

        var credits = new SortedDictionary<int, int>();
        foreach (var (id, (fire, by)) in incoming)
        {
            // The credit for ships lost goes to the fleets that fought them, shared.
            Fight.OnShips(game, fleets[id].Ships, fire, by, out var credit);
            foreach (var shooter in opponents[id])
            {
                credits[shooter.ObjectId] = credits.GetValueOrDefault(shooter.ObjectId) + (credit / opponents[id].Count);
            }
        }

        foreach (var id in opponents.Keys)
        {
            Fight.Experience([fleets[id]], credits.GetValueOrDefault(id), _settings);
        }

        Save(game, EngagedKey, [.. engaged.Where(p => fleets[p.Attacker].Ships.Count > 0 && fleets[p.Target].Ships.Count > 0)]);

        List<Fleet> Opponents(int id)
        {
            if (!opponents.TryGetValue(id, out var list))
            {
                opponents[id] = list = [];
            }

            return list;
        }
    }

    private void Sieges(Game game)
    {
        var planets = game.Universe.Stars.SelectMany(s => s.Planets).ToDictionary(p => p.ObjectId);
        var kept = new List<(int, int)>();
        foreach (var id in Pairs(game, SiegeKey).Select(p => p.Target).Distinct().Order())
        {
            if (!planets.TryGetValue(id, out var planet) || planet.Colony is not { } colony)
            {
                continue;
            }

            // Reinforcement: every enemy fleet circling the planet joins the
            // siege, and every fleet of the owner's or an ally's circling it
            // joins the defense, whoever sent it and whenever it came.
            var (besiegers, defenders) = Sides(game, planet, colony);
            if (besiegers.Count == 0)
            {
                continue;
            }

            Siege(game, colony, besiegers, defenders);
            var (left, defending) = Sides(game, planet, colony);
            if (left.Count == 0)
            {
                continue;
            }

            if (defending.Count == 0 && !Orbitals(colony).Any() && colony.CurrentPopulation <= _settings.CaptureAt)
            {
                game.CaptureColony(colony, left[0].Owner);
                continue;
            }

            kept.AddRange(left.Select(f => (f.ObjectId, id)));
        }

        Save(game, SiegeKey, kept);
    }

    /// <summary>
    /// One round at a besieged planet. The besiegers' fire falls on the
    /// defending fleets, then the orbitals (missile bases, battle stations,
    /// fighter bases), then the mine fields they can see, then the people.
    /// The defenders, the orbitals and the mines answer. Mines a besieger
    /// cannot see (no minesweeper of their level) strike in full and cannot
    /// be fired on, as in the original, where they are only shot at once seen.
    /// </summary>
    private void Siege(Game game, Colony colony, List<Fleet> besiegers, List<Fleet> defenders)
    {
        var attackers = Firepower.Of(game, besiegers);
        var fleets = Firepower.Of(game, defenders);
        var orbitals = Firepower.Of(game, Orbitals(colony), _settings);
        var guard = defenders.Count > 0 ? fleets : orbitals;
        var outgoing = Fight.Fire(attackers, guard, fleets.SmallCraftDefense(_settings) + orbitals.SmallCraftDefense(_settings), _settings);
        var returned = Fight.Fire(fleets, attackers, attackers.SmallCraftDefense(_settings), _settings)
            + Fight.Fire(orbitals, attackers, attackers.SmallCraftDefense(_settings), _settings);
        var seen = Fields(colony).Where(f => attackers.HighestSweeper >= game.MineFieldLevel(f.Tech)).ToList();
        var mines = Mines(game, colony, attackers);

        var left = Fight.OnShips(game, defenders.SelectMany(f => f.Ships), outgoing, besiegers[0].Owner, out var shipCredit);
        left = Fight.OnStructures(game, Orbitals(colony), left, out var orbitalCredit);
        Fight.OnStructures(game, seen, left);
        // The people are fired on only with no defender or orbital left, and,
        // as the original's planet blocks, every round lands: no hit chance.
        if (!defenders.Any(f => f.Ships.Count > 0) && !Orbitals(colony).Any())
        {
            var raw = attackers.Bombs + ((attackers.Beam + attackers.Missile + attackers.Drones) * _settings.OtherWeaponsOnPeople);
            game.Bombard(colony, raw * _settings.AttackShare * _settings.PopulationPerAttack);
        }

        Fight.OnShips(game, besiegers.SelectMany(f => f.Ships), returned + new Volley(0, mines, 0, 0), colony.Owner, out var besiegerCredit);
        Fight.Experience(besiegers, shipCredit + orbitalCredit, _settings);
        Fight.Experience(defenders, besiegerCredit, _settings);
    }

    /// <summary>
    /// The mines that strike the besiegers this round, as damage; the fields
    /// lose the mines spent and, when seen, those swept and shot down.
    /// </summary>
    private double Mines(Game game, Colony colony, Firepower besiegers, bool spend = true)
    {
        var damage = 0.0;
        foreach (var field in Fields(colony).ToList())
        {
            var mines = field.MaxHitPoints > 0 ? field.Count * _settings.MinesPerUnit * Math.Clamp(field.HitPoints / field.MaxHitPoints, 0, 1) : 0;
            var strength = Math.Max(1, field.Tech.InstallationStrength);
            var strikes = mines * _settings.MineStrikes;
            var cleared = 0.0;
            if (besiegers.HighestSweeper >= game.MineFieldLevel(field.Tech))
            {
                strikes *= _settings.SweptStrikes;
                cleared = (mines * _settings.SweepRate) + (besiegers.SmallCraftDefense(_settings) * _settings.AttackShare / strength);
            }

            damage += strikes * strength * _settings.MineDamageShare;
            if (spend)
            {
                game.DamageStructure(field, (strikes + cleared) * field.Tech.HitPoints / _settings.MinesPerUnit);
            }
        }

        return damage;
    }

    /// <summary>
    /// The chance shown beside the attack pointer: how long the attackers
    /// last under the defenders' fire against how long the defenders last
    /// under theirs, both at this round's strength, so even odds when the two
    /// would wear each other down together. At a planet the defenders' time
    /// runs on until the people are down too, and the mines strike all the
    /// while; the attacker's friends already circling it count with it.
    /// </summary>
    private double? Odds(Game game, Fleet fleet, ISpaceObject target)
    {
        var (attackers, defenders, orbitals, colony) = target switch
        {
            Fleet f => (new List<Fleet> { fleet }, new List<Fleet> { f }, new List<Structure>(), (Colony?)null),
            Planet { Colony: { } c } p => Planet(game, fleet, p, c),
            _ => (new List<Fleet>(), new List<Fleet>(), new List<Structure>(), null),
        };
        if (attackers.Count == 0)
        {
            return null;
        }

        var ours = Firepower.Of(game, attackers);
        var theirs = Firepower.Of(game, defenders);
        var guns = Firepower.Of(game, orbitals, _settings);
        var guard = defenders.Count > 0 ? theirs : guns;
        var dealt = Fight.Fire(ours, guard, theirs.SmallCraftDefense(_settings) + guns.SmallCraftDefense(_settings), _settings).Total;
        var taken = Fight.Fire(theirs, ours, ours.SmallCraftDefense(_settings), _settings).Total + Fight.Fire(guns, ours, ours.SmallCraftDefense(_settings), _settings).Total
            + (colony is null ? 0 : Mines(game, colony, ours, spend: false));
        var ourLife = attackers.SelectMany(f => f.Ships).Sum(s => s.Structure);
        var theirLife = defenders.SelectMany(f => f.Ships).Sum(s => s.Structure) + orbitals.Sum(o => o.HitPoints);
        var ourRounds = taken > 0 ? ourLife / taken : double.PositiveInfinity;
        var theirRounds = dealt > 0 ? theirLife / dealt : double.PositiveInfinity;
        if (colony is not null)
        {
            var people = (ours.Bombs + ((ours.Beam + ours.Missile + ours.Drones) * _settings.OtherWeaponsOnPeople)) * _settings.AttackShare * _settings.PopulationPerAttack;
            theirRounds += people > 0 ? Math.Max(0, colony.CurrentPopulation - _settings.CaptureAt) / people : double.PositiveInfinity;
        }

        return double.IsPositiveInfinity(theirRounds) ? 0
            : double.IsPositiveInfinity(ourRounds) ? 1
            : ourRounds / (ourRounds + theirRounds);
    }

    /// <summary>The two sides an attack on a colony would meet: the fleet with the friends circling the planet, and its defenders and orbitals.</summary>
    private static (List<Fleet>, List<Fleet>, List<Structure>, Colony?) Planet(Game game, Fleet fleet, Planet planet, Colony colony)
    {
        var (besiegers, _) = Sides(game, planet, colony);
        var attackers = besiegers.Where(f => f != fleet && (f.Owner == fleet.Owner || Allied(f.Owner, fleet.Owner))).Prepend(fleet).ToList();
        var defenders = game.Universe.Stars[colony.Star].Fleets
            .Where(f => f.Ships.Count > 0 && At(f, planet) && (f.Owner == colony.Owner || Allied(f.Owner, colony.Owner)))
            .OrderBy(f => f.ObjectId)
            .ToList();
        return (attackers, defenders, [.. Orbitals(colony)], colony);
    }

    /// <summary>A colony's defense structures but for its mine fields, in their order.</summary>
    private static IEnumerable<Structure> Orbitals(Colony colony) =>
        colony.Structures.Where(s => s.Tech.Kind is TechInfo.MissileBaseKind or TechInfo.BattleStationKind or TechInfo.FighterBaseKind);

    private static IEnumerable<Structure> Fields(Colony colony) => colony.Structures.Where(s => s.Tech.Kind == TechInfo.MineFieldKind);

    /// <summary>The planet lists' rating: what the orbitals deal a round before defenses, and the hit points of every defense structure.</summary>
    private ColonyRating Rate(Game game, Colony colony)
    {
        var orbitals = Firepower.Of(game, Orbitals(colony), _settings);
        var attack = (orbitals.Beam + orbitals.Missile + orbitals.Drones + orbitals.PointDefense) * _settings.AttackShare;
        var defense = Orbitals(colony).Concat(Fields(colony)).Sum(s => s.HitPoints);
        return new ColonyRating((int)Math.Round(attack), (int)Math.Round(defense));
    }

    /// <summary>The fleets circling a besieged planet: its enemies', and its owner's and allies' that are at war with one of them.</summary>
    private static (List<Fleet> Besiegers, List<Fleet> Defenders) Sides(Game game, Planet planet, Colony colony)
    {
        var circling = game.Universe.Stars[colony.Star].Fleets.Where(f => f.Ships.Count > 0 && At(f, planet)).OrderBy(f => f.ObjectId).ToList();
        var besiegers = circling.Where(f => Game.AtWar(f.Owner, colony.Owner)).ToList();
        var defenders = circling.Where(f => (f.Owner == colony.Owner || Allied(f.Owner, colony.Owner)) && besiegers.Any(b => Game.AtWar(b.Owner, f.Owner))).ToList();
        return (besiegers, defenders);
    }

    /// <summary>
    /// A fleet circling a planet, or idle on it: a computer player's new
    /// fleet sits where its shipyard's planet was instead of circling it.
    /// </summary>
    private static bool At(Fleet fleet, Planet planet) =>
        fleet.OrbitCenter == planet
        || (fleet.OrbitCenter is null && fleet.Orders.Count == 0 && Math.Abs(fleet.X - planet.X) <= NearPlanet && Math.Abs(fleet.Y - planet.Y) <= NearPlanet);

    private static bool Allied(Player a, Player b) => a.Treaties[b.Index].Has(Treaty.Allied) && b.Treaties[a.Index].Has(Treaty.Allied);

    private static List<(int Attacker, int Target)> Pairs(Game game, string key) =>
        game.ModState.TryGetValue(key, out var text) && text.Length > 0
            ? [.. text.Split(';').Select(p => p.Split(':')).Where(p => p.Length == 2).Select(p => (int.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)))]
            : [];

    private static void Save(Game game, string key, List<(int Attacker, int Target)> engaged)
    {
        if (engaged.Count == 0)
        {
            game.ModState.Remove(key);
        }
        else
        {
            game.ModState[key] = string.Join(';', engaged.Select(e => e.Attacker.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + e.Target.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }
}
