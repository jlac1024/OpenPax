using System.Globalization;
using OpenPax.Sim.Players;

namespace OpenPax.Sim.Setup;

/// <summary>Creating the players at a new game; see docs/sim/players.md.</summary>
internal static class PlayerCreation
{
    private const int FlagCount = 30;
    private const int FleetColorCount = 16;
    private const int AiNameCount = 30;
    private const int SpeciesChoices = 8;
    private const int StartingAdvisors = 4;
    private const int GovernorRole = 5;

    // AI design slots 0..5 hold one design of each type from the start.
    private const int AiStartingDesigns = 6;

    private const int HumanStartingDesigns = 3;

    // Learning a tech of these kinds (improvements and installations) does not redesign.
    private static readonly int[] _noRedesignKinds = [-2, 5, 6, 7, 8, 9, 10];

    public static void CreatePlayers(Game game, GameTables tables)
    {
        var setup = game.Setup;
        var random = game.Random;
        var flags = new bool[FlagCount];
        var fleets = new bool[FleetColorCount];
        var names = new bool[AiNameCount];

        // Human names are not marked, only their flags and fleet colors.
        foreach (var h in setup.Humans)
        {
            flags[h.Flag] = true;
            fleets[h.FleetColor] = true;
        }

        var choices = new List<HumanPlayerSetup>(setup.Humans);
        for (var i = setup.Humans.Count; i < setup.TotalPlayers; i++)
        {
            var species = tables.Species[random.Next(SpeciesChoices)];
            var fleet = random.Next(FleetColorCount);
            var flag = random.Next(FlagCount);
            int name;
            do
            {
                name = random.Next(AiNameCount);
            }
            while (names[name]);

            names[name] = true;
            while (flags[flag])
            {
                flag = random.Next(FlagCount);
            }

            flags[flag] = true;
            while (fleets[fleet])
            {
                fleet = random.Next(FleetColorCount);
            }

            fleets[fleet] = true;
            choices.Add(new HumanPlayerSetup(tables.AiNames[name], flag, fleet, species));
        }

        for (var i = 0; i < choices.Count; i++)
        {
            var isAi = i >= setup.Humans.Count;
            var c = choices[i];
            var player = new Player(i, isAi, c.Name, c.Flag, c.FleetColor, c.Species);
            if (isAi)
            {
                for (var t = 0; t < AiStartingDesigns; t++)
                {
                    // Blank designs from the AI constructor; no draws, no ids.
                    player.Designs[t] = new Design(tables.Hulls[t]) { Slot = t };
                }
            }

            Initialize(game, player, tables);
            if (isAi)
            {
                GiveAiStartingTechs(game, player, tables);
                SplitAiResearch(player, random);
            }
            else
            {
                CreateHumanDesigns(game, player);
            }

            game.Players.Add(player);
        }
    }

    /// <summary>A starting or governor advisor: portrait draw, then the skill draw or a fixed governor value.</summary>
    public static Advisor NewAdvisor(Game game, Player player, int role, int star = Advisor.NoStar)
    {
        var portrait = (player.Species.Picture + 1) * 1000 + game.Random.Next(10);
        var id = game.SecondaryIds.Take();
        var skills = new double[4];
        var politician = player.Species.Is(Species.Politician);
        if (role == GovernorRole)
        {
            Array.Fill(skills, politician ? 0.04 : 0.02);
        }
        else
        {
            skills[role] = (game.Random.Next(5) + (politician ? 8 : 6)) * 0.01;
        }

        return new Advisor(id, role, portrait, skills) { Star = star };
    }

    /// <summary>
    /// Rebuilds every design from a blank one of the same type, unless the
    /// tech just learned is an improvement or installation. The builder
    /// draws only for computer players' type 1 and 3 designs. Rebuilt
    /// designs have no armor.
    /// </summary>
    public static void RebuildAiDesigns(Game game, Player player, TechInfo? learned)
    {
        if (learned is not null && _noRedesignKinds.Contains(learned.Kind))
        {
            return;
        }

        for (var slot = 0; slot < Player.DesignSlots; slot++)
        {
            if (player.Designs[slot] is not { } old)
            {
                continue;
            }

            player.Designs[slot] = null;
            var design = new Design(game.Tables.Hulls[old.Type]);
            DesignBuilder.Build(game, old.Type, design, player);
            AddDesign(game, player, design);
        }
    }

    /// <summary>A human starts with designs of types 0 to 2, each with the best armor fitted first.</summary>
    private static void CreateHumanDesigns(Game game, Player player)
    {
        for (var type = 0; type < HumanStartingDesigns; type++)
        {
            var design = new Design(game.Tables.Hulls[type]) { Owner = player };
            if (player.BestArmor > 0)
            {
                design.Add(game.Tables.Tech(player.BestArmor), Component.NotWeapon);
            }

            design.Name = game.Tables.DesignNames[type + 27];
            DesignBuilder.Build(game, type, design, player);
            AddDesign(game, player, design);
        }
    }

    /// <summary>Puts a design in the first free slot with a class name and a new id.</summary>
    internal static void AddDesign(Game game, Player player, Design design)
    {
        var slot = Array.IndexOf(player.Designs, null);
        if (slot < 0)
        {
            return;
        }

        design.Owner = player;
        design.Recalculate();
        design.Slot = slot;
        var counter = player.DesignCounters[design.Type]++;
        design.ClassName = game.Tables.ClassNames[design.Type] + counter.ToString(CultureInfo.InvariantCulture);
        design.Id = game.SecondaryIds.Take();
        player.Designs[slot] = design;
    }

    private static void Initialize(Game game, Player player, GameTables tables)
    {
        for (var role = 0; role < StartingAdvisors; role++)
        {
            // Each new advisor goes on the front of the list.
            player.Advisors.Insert(0, NewAdvisor(game, player, role));
        }

        // Every tech that costs nothing to research is known, whatever its
        // prerequisites (Robotic Mining is given although it needs 199).
        // free techs, then the first
        // research of every field.
        foreach (var t in tables.Techs)
        {
            if (TechLevels.ResearchCost(t, player) == 0)
            {
                game.GiveTech(player, t);
            }
        }

        for (var field = 0; field < player.ResearchSlots.Length; field++)
        {
            game.PickResearch(player, field, false);
        }

        for (var field = 0; field < player.ResearchSlots.Length; field++)
        {
            if (player.ResearchSlots[field].Tech == ResearchSlot.None)
            {
                game.FieldExhausted(player, field);
            }
        }

        // After the species is set, so a builder's discount counts.
        player.Plans = ColonyPlans.Build(tables, player);
        player.BestArmor = BestKnown(player, tables, TechInfo.ArmorKind);
    }

    private static void GiveAiStartingTechs(Game game, Player player, GameTables tables)
    {
        for (var id = 1; id <= 3; id++)
        {
            if (!player.Knows(id))
            {
                // A slot already researching it keeps going (quirk kept).
                var tech = tables.Tech(id);
                game.GiveTech(player, tech);
                game.OnTechLearned(player, tech);
            }
        }
    }

    /// <summary>
    /// Shares out research by weighted draws, then stores each field through
    /// the same clamp the original uses, against allocations still at their
    /// starting 20: weapons ends at most 20 and the total can stay under 100.
    /// </summary>
    private static void SplitAiResearch(Player player, GameRandom random)
    {
        var species = player.Species;
        var levels = species.ResearchLevels;
        var thresholds = new int[5];
        var running = 0;
        for (var c = 0; c < 5; c++)
        {
            running += levels[c] + 2;
            thresholds[c] = running;
        }

        var weights = new[] { 10, 10, 10, 10, 10 };
        var total = 50;
        while (total < 100)
        {
            var r = random.Next(thresholds[4]) + 1;
            var c = 0;
            while (r > thresholds[c])
            {
                c++;
            }

            var favored = c switch
            {
                0 => species.Is(Species.Warrior) || species.Has(Species.Predator),
                1 => species.Is(Species.Warrior),
                2 => species.Is(Species.Nomad),
                3 => species.Is(Species.Builder) || species.Is(Species.Spy),
                _ => species.Is(Species.Builder),
            };
            var amount = favored ? random.Next(7) + 1 : random.Next(3) + 1;
            amount = Math.Min(amount, 100 - total);
            weights[c] += amount;
            total += amount;
        }

        for (var c = 0; c < 5; c++)
        {
            var others = player.Research.Sum() - player.Research[c];
            player.Research[c] = Math.Min(weights[c], Math.Max(0, 100 - others));
        }
    }

    /// <summary>The highest-id known tech of a kind, or 0.</summary>
    public static int BestKnown(Player player, GameTables tables, int kind)
    {
        var best = 0;
        foreach (var t in tables.Techs)
        {
            if (t.Kind == kind && player.KnownTechs.Contains(t.Id))
            {
                best = t.Id;
            }
        }

        return best;
    }
}
