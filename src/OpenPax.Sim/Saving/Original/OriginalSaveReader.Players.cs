using OpenPax.Sim.Players;

namespace OpenPax.Sim.Saving.Original;

/// <summary>The player block ( written read).</summary>
internal sealed partial class OriginalSaveReader
{
    private Player ReadPlayer(bool isAi)
    {
        // the empire block comes before the species the
        // player is made with, so it is held until then.
        var name = _in.String();
        var flag = _in.Int32();
        var color = _in.Int32();
        var index = _in.Int32();
        var treaties = new int[Player.MaxPlayers][];
        var scores = new double[Player.MaxPlayers];
        var tradeRamp = new double[Player.MaxPlayers];
        var tradeReceipts = new double[Player.MaxPlayers];
        var researchRamp = new double[Player.MaxPlayers];
        var researchReceipts = new double[Player.MaxPlayers];
        for (var j = 0; j < Player.MaxPlayers; j++)
        {
            treaties[j] = new int[Treaty.States];
            for (var k = 0; k < Treaty.States; k++)
            {
                treaties[j][k] = _in.Int32();
            }

            _in.End();
            scores[j] = _in.Double();
            tradeRamp[j] = _in.Double();
            tradeReceipts[j] = _in.Double();
            researchRamp[j] = _in.Double();
            researchReceipts[j] = _in.Double();
        }

        var spending = new[] { _in.Double(), _in.Double(), _in.Double() };
        var improvementUpkeep = new[] { _in.Double(), _in.Double(), _in.Double(), _in.Double(), _in.Double() };
        var income = _in.Double();
        var eliminated = _in.Bool();
        _in.End();

        var p = new Player(index, isAi, name, flag, color, ReadSpecies());
        for (var j = 0; j < Player.MaxPlayers; j++)
        {
            for (var k = 0; k < Treaty.States; k++)
            {
                p.Treaties[j].Restore(k, treaties[j][k]);
            }
        }

        scores.CopyTo(p.RelationScores, 0);
        tradeRamp.CopyTo(p.TradeRamp, 0);
        tradeReceipts.CopyTo(p.TradeReceipts, 0);
        researchRamp.CopyTo(p.ResearchRamp, 0);
        researchReceipts.CopyTo(p.ResearchReceipts, 0);
        spending.CopyTo(p.Spending, 0);
        improvementUpkeep.CopyTo(p.ImprovementUpkeep, 0);
        p.Income = income;
        p.Eliminated = eliminated;

        for (var t = 0; t < p.DesignCounters.Length; t++)
        {
            p.DesignCounters[t] = _in.Int32();
        }

        p.BestArmor = _in.Int32();
        p.Money = _in.Double();
        p.ResearchPoints = _in.Double();
        p.EspionagePoints = _in.Double();
        ReadPlans(p);
        ReadResearch(p);
        ReadMissions(p);
        p.FleetCounter = _in.Int32();
        p.Upkeep = _in.Double();
        for (var k = 0; k < 3; k++)
        {
            p.StructureUpkeep[k] = _in.Double();
        }

        p.Support = _in.Double();
        p.Funding = _in.Double();

        // History, interleaved by sample: each sample holds all ten series.
        for (var sample = 0; sample < EmpireHistory.Samples; sample++)
        {
            for (var s = 0; s < 10; s++)
            {
                p.History.Restore(s, sample, _in.Int32());
            }
        }

        var packages = _in.Int32();
        for (var i = 0; i < packages; i++)
        {
            ReadPackage(p);
        }

        var advisors = _in.Int32();
        for (var i = 0; i < advisors; i++)
        {
            p.Advisors.Add(ReadAdvisor());
        }

        var colonies = _in.Int32();
        for (var i = 0; i < colonies; i++)
        {
            p.Colonies.Add(ReadColony(p));
        }

        var fleets = _in.Int32();
        for (var i = 0; i < fleets; i++)
        {
            p.Fleets.Add(ReadFleet(p));
        }

        var designs = _in.Int32();
        for (var i = 0; i < designs; i++)
        {
            var slot = _in.Int32();
            var design = ReadDesign(p);
            design.Slot = slot;
            p.Designs[slot] = design;
        }

        // The design being made in the Ship Design window: read and dropped,
        // as the port keeps it with the screens.
        if (_in.Int32() != -1)
        {
            ReadDesign(p);
        }

        ReadInterfaceSettings();
        _in.Byte();
        p.Caution = _in.Double();
        for (var j = 0; j < Player.MaxPlayers; j++)
        {
            p.Relations[j] = _in.Int32();
        }

        _in.End();
        return p;
    }

    /// <summary>The species copy (the race template layout); no end marker.</summary>
    private Species ReadSpecies()
    {
        var raceId = _in.Int32();
        var name = _in.String();
        _in.String();
        var picture = _in.Int32();
        var temperature = _in.Int32();
        var tolerance = _in.Int32();
        bool[] Flags(int n) => [.. Enumerable.Range(0, n).Select(_ => _in.Bool())];
        sbyte[] Signed(int n) => [.. Enumerable.Range(0, n).Select(_ => _in.SByte())];
        var atmospheres = Flags(4);
        var physical = Flags(5);
        var cultural = Flags(7);
        var rates = Signed(5);
        var research = Signed(5);
        return new Species(raceId, name, picture, temperature, tolerance, atmospheres, physical, cultural, rates, research);
    }

    /// <summary>
    /// The plan library: the count of plans, then slots 0 up to
    /// that count. Quirk kept from the writer: a gap among the slots would
    /// lose the plans after it.
    /// </summary>
    private void ReadPlans(Player p)
    {
        var count = _in.Int32();
        for (var slot = 0; slot < count; slot++)
        {
            var name = _in.String();
            var ids = new int[50];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = _in.Int32();
            }

            // +0xD8 is probably the id count; every plan the game makes is full,
            // so the ids are taken as they are, empty entries left out.
            _in.Int32();
            _in.End();
            p.Plans[slot] = [.. ids.Where(id => id > 0)];
            p.PlanNames[slot] = slot < 6 ? null : name;
        }

        _in.End();
    }

    /// <summary>The research manager.</summary>
    private void ReadResearch(Player p)
    {
        var slots = _in.Int32();
        for (var i = 0; i < slots; i++)
        {
            var tech = _in.Int32();
            var cost = _in.Int32();
            var progress = _in.Int32();
            var fraction = _in.Double();
            _in.End();
            if (i < p.ResearchSlots.Length)
            {
                var slot = p.ResearchSlots[i];
                (slot.Tech, slot.Cost, slot.Progress, slot.Fraction) = (tech, cost, progress, fraction);
            }
        }

        // Progress kept for techs no longer a project, dense by tech id.
        var kept = _in.Int32();
        for (var id = 0; id < kept; id++)
        {
            var progress = _in.Int32();
            if (progress != 0)
            {
                p.SavedProgress[id] = progress;
            }
        }

        var bits = _in.Bytes((KnownTechBits + 7) / 8);
        for (var id = 0; id < KnownTechBits; id++)
        {
            if ((bits[id >> 3] & (1 << (id & 7))) != 0)
            {
                p.KnownTechs.Add(id);
            }
        }

        for (var f = 0; f < p.Research.Length; f++)
        {
            p.Research[f] = _in.Int32();
        }

        p.KnownCount = _in.Int32();
        p.ResearchTotal = _in.Int32();
        p.AutoResearch = _in.Bool();
        _in.End();
    }

    /// <summary>The player's spy missions; the mission counter follows them.</summary>
    private void ReadMissions(Player p)
    {
        var count = _in.Int32();
        for (var i = 0; i < count; i++)
        {
            var target = _in.Int32();
            _in.Int32();
            var kind = _in.Int32();
            _in.Double();
            var paid = _in.Double();
            var level = _in.Int32();
            var id = _in.Int32();
            _in.End();
            _relinks.Add(() => p.Missions.Add(new SpyMission(id, p, PlayerAt(target), kind, level) { Progress = paid }));
        }

        var next = _in.Int32();
        _relinks.Add(() => _nextMission = next);
        _in.End();
    }

    private int _nextMission;

    /// <summary>The player object of the interface settings, not kept by the port.</summary>
    private void ReadInterfaceSettings()
    {
        for (var i = 0; i < 5; i++)
        {
            _in.Byte();
        }

        for (var i = 0; i < 11; i++)
        {
            _in.Int32();
        }

        _in.Byte();
        for (var i = 0; i < 4; i++)
        {
            _in.Int32();
        }

        _in.Bytes(16);
        _in.End();
    }
}
