namespace OpenPax.Sim.Players;

/// <summary>A player, human or computer (base constructor).</summary>
public sealed class Player(int index, bool isAi, string name, int flag, int fleetColor, Species species)
{
    public const int MaxPlayers = 16;
    public const int DesignSlots = 100;
    public const double StartingMoney = 5000.0;

    public int Index { get; } = index;

    public bool IsAi { get; } = isAi;

    public string Name { get; } = name;

    public int Flag { get; } = flag;

    public int FleetColor { get; } = fleetColor;

    public Species Species { get; } = species;

    public List<Advisor> Advisors { get; } = [];

    /// <summary>Espionage missions this player runs, paid for in order (the EspionageMan list, +0x70C).</summary>
    public List<SpyMission> Missions { get; } = [];

    /// <summary>Trade packages offered to this player and not yet answered, newest first (+0x800).</summary>
    public List<TradePackage> Packages { get; } = [];

    /// <summary>
    /// A computer player's patience with demands, by proposer: 30 at the
    /// start, 10 less for every demand, never raised (its treaty module, +8).
    /// </summary>
    public double[] Patience { get; } = Enumerable.Repeat(30.0, MaxPlayers).ToArray();

    public double Money { get; internal set; } = StartingMoney;

    /// <summary>Research allocation per field (weapons, shielding, ship, colonial, space), in percent.</summary>
    public int[] Research { get; } = [20, 20, 20, 20, 20];

    public SortedSet<int> KnownTechs { get; } = [];

    /// <summary>Highest-id armor the player knows, or 0.</summary>
    public int BestArmor { get; internal set; }

    /// <summary>Design slots; null where empty.</summary>
    public Design?[] Designs { get; } = new Design?[DesignSlots];

    /// <summary>Attitude to each player slot, -3 to 3.</summary>
    public int[] Relations { get; } = new int[MaxPlayers];

    /// <summary>A second per-player value from relations plus a draw (+0x210; meaning not yet known).</summary>
    public double[] RelationScores { get; } = new double[MaxPlayers];

    public int HomeStar { get; internal set; } = -1;

    public int HomePlanetSlot { get; internal set; } = -1;

    public List<Colony> Colonies { get; } = [];

    public List<Fleet> Fleets { get; } = [];

    /// <summary>The design AUTO last built for the Ship Design window to take up; not part of the state hash.</summary>
    public Design? Draft { get; internal set; }

    /// <summary>How many drafts AUTO has made, so a network client's screen knows when the one it asked for has come; not part of the state hash.</summary>
    public int DraftNumber { get; internal set; }

    /// <summary>Numbers fleets in their names (+0x76C).</summary>
    internal int FleetCounter { get; set; }

    /// <summary>
    /// Sum of the raw research costs of every tech given to the player
    /// (tech state +0x48); its level scales component sizes and costs.
    /// </summary>
    public int ResearchTotal { get; internal set; }

    /// <summary>Numbers designs in their class names, per design type (+0x770).</summary>
    public int[] DesignCounters { get; } = new int[6];

    /// <summary>Upkeep of all ships (+0x790): their costs, times 0.8 for nomads.</summary>
    public double Upkeep { get; internal set; }

    /// <summary>Upkeep of shipyards (+0x798), of kind 6 structures (+0x7A0) and of every other structure (+0x7A8).</summary>
    public double[] StructureUpkeep { get; } = new double[3];

    /// <summary>Upkeep of completed improvements by resource (+0x4C0).</summary>
    public double[] ImprovementUpkeep { get; } = new double[5];

    /// <summary>Spending percents for research, espionage and construction (+0x4A8), 0 to 100.</summary>
    public double[] Spending { get; } = [100.0, 100.0, 100.0];

    /// <summary>Research points of the period (+0x718).</summary>
    public double ResearchPoints { get; internal set; }

    /// <summary>Espionage of the period (+0x720): per head after the economy pass.</summary>
    public double EspionagePoints { get; internal set; }

    /// <summary>Credits income of the last period (+0x4E8).</summary>
    public double Income { get; internal set; }

    /// <summary>
    /// How much stronger than a target a computer player's fleet must be to
    /// attack it (+0x560). Starts at 1.1 and rises 0.2 with each retreat and
    /// each fleet lost in combat.
    /// </summary>
    public double Caution { get; internal set; } = 1.1;

    /// <summary>Percent of upkeep that could be paid (+0x7B0); below 100 things decay.</summary>
    public double Support { get; internal set; } = 100.0;

    /// <summary>Sum of the colonies' construction funding (+0x7F8).</summary>
    public double Funding { get; internal set; }

    /// <summary>Per other player: trade treaty ramp 0 to 1 (+0x290) and credits received (+0x310).</summary>
    public double[] TradeRamp { get; } = new double[MaxPlayers];

    public double[] TradeReceipts { get; } = new double[MaxPlayers];

    /// <summary>Per other player: research treaty ramp (+0x390) and research received (+0x410).</summary>
    public double[] ResearchRamp { get; } = new double[MaxPlayers];

    public double[] ResearchReceipts { get; } = new double[MaxPlayers];

    /// <summary>Samples of ten statistics for the comparison graphs.</summary>
    public EmpireHistory History { get; } = new();

    /// <summary>One slot per research field.</summary>
    public ResearchSlot[] ResearchSlots { get; } = [new(), new(), new(), new(), new()];

    /// <summary>Progress kept for techs whose research was switched away by hand (tech state +0x14), by tech id.</summary>
    public Dictionary<int, int> SavedProgress { get; } = [];

    /// <summary>Techs learned so far, counting a tech learned twice twice (+0x40).</summary>
    public int KnownCount { get; internal set; }

    /// <summary>Pick the next research of a field on its own when one completes (+0x44).</summary>
    public bool AutoResearch { get; internal set; } = true;

    /// <summary>Known, as the original asks it: tech 0 always, -1 never.</summary>
    public bool Knows(int tech)
    {
        return tech switch
        {
            -1 => false,
            0 => true,
            _ => KnownTechs.Contains(tech),
        };
    }

    public const int PlanSlots = 10;

    /// <summary>
    /// The plan library (+0x700): ten slots of improvement tech ids in build
    /// order. Slots 0 to 5 hold the stock plans; 6 to 9 the
    /// player's own, null when empty.
    /// </summary>
    public int[]?[] Plans { get; internal set; } = new int[]?[PlanSlots];

    /// <summary>The names of the player's own plans (plan +4); null for the stock ones, which the screens name from STR# 103.</summary>
    public string?[] PlanNames { get; } = new string?[PlanSlots];

    /// <summary>
    /// Out of the game (+0x4F0), set by elimination: computer
    /// players with it set are skipped by the AI task.
    /// </summary>
    public bool Eliminated { get; internal set; }

    /// <summary>Treaty states toward each player slot.</summary>
    public Treaty[] Treaties { get; } = [.. Enumerable.Range(0, MaxPlayers).Select(_ => new Treaty())];

    public void AddTo(StateHash hash)
    {
        hash.Add(Index).Add(IsAi ? 1 : 0).Add(Eliminated ? 1 : 0).Add(Name).Add(Flag).Add(FleetColor).Add(Species.RaceId);
        foreach (var a in Advisors)
        {
            a.AddTo(hash);
        }

        hash.Add(Money).Add(BestArmor);
        foreach (var r in Research)
        {
            hash.Add(r);
        }

        hash.Add(KnownTechs.Count);
        foreach (var t in KnownTechs)
        {
            hash.Add(t);
        }

        hash.Add(Caution).Add(ResearchTotal).Add(Upkeep).Add(ResearchPoints).Add(EspionagePoints).Add(Income).Add(Support).Add(Funding);
        foreach (var v in StructureUpkeep.Concat(ImprovementUpkeep).Concat(Spending).Concat(TradeRamp).Concat(TradeReceipts).Concat(ResearchRamp).Concat(ResearchReceipts))
        {
            hash.Add(v);
        }

        hash.Add(KnownCount).Add(AutoResearch ? 1 : 0);
        History.AddTo(hash);
        foreach (var slot in ResearchSlots)
        {
            slot.AddTo(hash);
        }

        hash.Add(Missions.Count);
        foreach (var m in Missions)
        {
            m.AddTo(hash);
        }

        hash.Add(Packages.Count);
        foreach (var k in Packages)
        {
            k.AddTo(hash);
        }

        foreach (var v in Patience)
        {
            hash.Add(v);
        }

        foreach (var plan in Plans.OfType<int[]>())
        {
            foreach (var id in plan)
            {
                hash.Add(id);
            }
        }
        foreach (var n in DesignCounters)
        {
            hash.Add(n);
        }

        for (var i = 0; i < DesignSlots; i++)
        {
            if (Designs[i] is { } d)
            {
                hash.Add(i);
                d.AddTo(hash);
            }
        }

        for (var i = 0; i < MaxPlayers; i++)
        {
            hash.Add(Relations[i]).Add(RelationScores[i]);
            Treaties[i].AddTo(hash);
        }

        hash.Add(HomeStar).Add(HomePlanetSlot);
        foreach (var c in Colonies)
        {
            c.AddTo(hash);
        }

        foreach (var f in Fleets)
        {
            f.AddTo(hash);
        }
    }
}
