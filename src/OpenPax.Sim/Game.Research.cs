using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim;

/// <summary>The research task and choosing research (docs/sim/research.md).</summary>
public sealed partial class Game
{
    private const int ResearchFields = 5;
    private const int SpaceField = 4;
    private const int ColonialField = 3;
    private const int AcademyKind = 6;

    // Techs a computer player at difficulty 3 or more goes for first in the shielding and ship fields.
    private static readonly int[] _favoredResearch = [39, 42, 43, 46, 50, 51, 186, 187, 188];

    // Colonial ties go to the better resource: research, construction, credits, espionage, population.
    private static readonly int[] _colonialRank = [2, 0, 3, 1, 4];

    private void RunResearchTask()
    {
        foreach (var player in Players)
        {
            Research(player);
        }
    }

    private void Research(Player p)
    {
        for (var field = 0; field < ResearchFields; field++)
        {
            var slot = p.ResearchSlots[field];
            if (slot.Tech == ResearchSlot.None)
            {
                continue;
            }

            AddResearchPoints(p, slot);
            if (!slot.Done)
            {
                continue;
            }

            var tech = Tables.Tech(slot.Tech);
            GiveTech(p, tech);
            slot.Clear();
            OnTechLearned(p, tech);

            // with the field index: the notice.
            Notify(new GameNotice(NoticeKind.ResearchCompleted, p, tech.Id));
            if (p.Species.Is(Species.Scientist))
            {
                ScientistBonus(p, tech);
            }

            if (p.AutoResearch)
            {
                PickResearch(p, tech.ResearchField, true);
            }
        }
    }

    /// <summary>The field's share of the research points; whole points count, the fraction carries.</summary>
    private void AddResearchPoints(Player p, ResearchSlot slot)
    {
        var field = Tables.Tech(slot.Tech).ResearchField;
        var points = p.Research[field] * (p.ResearchPoints * 10.0) * 0.01 * 0.1;
        var whole = X87.ToInt(points);
        var fraction = points - whole + slot.Fraction;
        if (1.0 <= fraction)
        {
            slot.Fraction = fraction - 1.0;
            slot.Progress += whole + 1;
        }
        else
        {
            slot.Fraction = fraction;
            slot.Progress += whole;
        }
    }

    /// <summary>
    /// Learns a tech: known, counted, its raw cost added to the research
    /// total, and an improvement gets a slot in every colony.
    /// </summary>
    internal void GiveTech(Player p, TechInfo tech)
    {
        p.KnownTechs.Add(tech.Id);
        p.KnownCount++;
        if (tech.ClassId == TechInfo.ImprovementClass)
        {
            foreach (var c in p.Colonies)
            {
                c.Improvements[ImprovementSlot(tech)] ??= new Improvement(tech);
            }
        }

        p.ResearchTotal += TechLevels.ResearchCost(tech, null);
    }

    /// <summary>
    /// Learns a tech from outside research (espionage): a research field
    /// working on it stops and, with auto research, picks again.
    /// </summary>
    internal void LearnTech(Player p, TechInfo tech)
    {
        if (p.Knows(tech.Id))
        {
            return;
        }

        GiveTech(p, tech);
        OnTechLearned(p, tech);
        foreach (var slot in p.ResearchSlots)
        {
            if (slot.Tech != tech.Id)
            {
                continue;
            }

            // Quirk kept: the field's notice (virtual +0xC8) runs
            // the learned effects a second time, AI redesigns included.
            slot.Clear();
            OnTechLearned(p, tech);

            // Quirk kept: a tech stolen while it was being researched reads as a breakthrough.
            Notify(new GameNotice(NoticeKind.Breakthrough, p, tech.Id));
            if (p.AutoResearch)
            {
                PickResearch(p, tech.ResearchField, true);
            }
        }
    }

    /// <summary>
    /// What a player does on learning a tech: structures take it up, the
    /// best armor and ship upkeep are worked out again, and designs change.
    /// </summary>
    internal void OnTechLearned(Player p, TechInfo tech)
    {
        Happen(new ModEvent(ModEventKind.TechLearned, p, Tech: tech));
        GivePremadeDesigns(p);
        foreach (var c in p.Colonies)
        {
            foreach (var s in c.Structures)
            {
                FitStructure(s, tech);
            }
        }

        p.BestArmor = PlayerCreation.BestKnown(p, Tables, TechInfo.ArmorKind);

        p.Upkeep = 0.0;
        foreach (var fleet in p.Fleets)
        {
            foreach (var ship in fleet.Ships)
            {
                var cost = (double)ship.Cost;
                p.Upkeep = (p.Species.Is(Species.Nomad) ? cost * 0.8 : cost) + p.Upkeep;
            }
        }

        if (p.IsAi)
        {
            PlayerCreation.RebuildAiDesigns(this, p, tech);
        }
        else if (p.BestArmor > 0)
        {
            foreach (var design in p.Designs.OfType<Design>())
            {
                design.Add(Tables.Tech(p.BestArmor), Component.NotWeapon);
            }
        }
    }

    /// <summary>
    /// Fits a tech to a structure: a shipyard, kind 6 or kind 10 takes a
    /// tech of its own kind; missile bases, battle stations and fighter
    /// bases take newer weapons, fighters, ECM, sensors and shields.
    /// </summary>
    internal void FitStructure(Structure s, TechInfo t)
    {
        if (t.Kind == TechInfo.ImprovementKind)
        {
            return;
        }

        static bool Better(TechInfo? slot, TechInfo t) => slot is null || slot.Id < t.Id;

        var kind = s.Tech.Kind;
        if ((t.Kind is TechInfo.ShipyardKind or TechInfo.TrainingBaseKind or 10) && kind == t.Kind)
        {
            s.Tech = t;
        }
        else if (kind == TechInfo.BattleStationKind)
        {
            if (t.Kind is TechInfo.EnergyWeaponKind or TechInfo.AreaWeaponKind && t.WeaponFlag == 0xFF && Better(s.WeaponTechs[0], t))
            {
                s.WeaponTechs[0] = t;
            }
            else if (t.Kind == TechInfo.ProjectileKind && t.WeaponFlag == 0xFF && Better(s.WeaponTechs[1], t))
            {
                s.WeaponTechs[1] = t;
            }
        }
        else if (kind == 9 && t.Kind == TechInfo.FighterKind)
        {
            var d = Tables.IndexInKind(Tables.Tech(t.LookId));
            var slot = d < 6 ? 0 : d < 12 ? 1 : 2;
            if (Better(s.FighterTechs[slot], t))
            {
                s.FighterTechs[slot] = t;
            }
        }
        else if (kind == TechInfo.MissileBaseKind && t.Kind == TechInfo.MissileKind)
        {
            var slot = t.Damage[3] == 0 ? 0 : 1;
            if (Better(s.WeaponTechs[slot], t))
            {
                s.WeaponTechs[slot] = t;
            }
        }

        if (kind is not (TechInfo.MissileBaseKind or TechInfo.BattleStationKind or 9))
        {
            return;
        }

        switch (t.Kind)
        {
            case TechInfo.EcmKind when Better(s.EcmTech, t):
                s.EcmTech = t;
                break;
            case TechInfo.SensorKind when Better(s.SensorTech, t):
                s.SensorTech = t;
                break;
            case TechInfo.ScreenKind or TechInfo.ShieldKind or TechInfo.FieldKind:
                if (s.ShieldTech is null || TechLevels.ResearchCost(s.ShieldTech, s.Colony.Owner) < TechLevels.ResearchCost(t, s.Colony.Owner))
                {
                    s.ShieldTech = t;
                }

                break;
            case TechInfo.EnergyWeaponKind when Better(s.SecondEnergyTech, t):
                s.SecondEnergyTech = t;
                break;
        }
    }

    /// <summary>One time in ten a scientist species also gets, free, a tech that needs the one just learned.</summary>
    private void ScientistBonus(Player p, TechInfo learned)
    {
        if (Random.Next(10) != 0)
        {
            return;
        }

        // The original collects these in an array of ten without a bound check.
        var candidates = Tables.Techs
            .Where(t => !p.Knows(t.Id) && CanResearch(p, t) && t.Prerequisites.Contains(learned.Id))
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var bonus = candidates[Random.Next(candidates.Count)];
        GiveTech(p, bonus);
        OnTechLearned(p, bonus);

        Notify(new GameNotice(NoticeKind.Breakthrough, p, bonus.Id));
    }

    /// <summary>Not known, prerequisites known, and no academy for warriors.</summary>
    private static bool CanResearch(Player p, TechInfo tech)
    {
        if (p.Knows(tech.Id) || (tech.Kind == AcademyKind && p.Species.Is(Species.Warrior)))
        {
            return false;
        }

        return tech.Prerequisites.All(p.Knows);
    }

    /// <summary>Techs of a field that can be researched now and are not in a slot already.</summary>
    private IEnumerable<TechInfo> Open(Player p, int field)
    {
        return Tables.Techs.Where(t => !p.Knows(t.Id) && t.ResearchField == field
            && !p.ResearchSlots.Any(s => s.Tech == t.Id) && CanResearch(p, t));
    }

    /// <summary>Chooses the next research of a field and starts it.</summary>
    internal void PickResearch(Player p, int field, bool fallBack)
    {
        int? pick;
        if (p.IsAi && Setup.Difficulty >= 3)
        {
            pick = FavoredResearch(p, field) ?? MostExpensiveResearch(p, field) ?? CheapestResearch(p, field, true) ?? CheapestResearch(p, field, false);
        }
        else
        {
            pick = CheapestResearch(p, field, false);
        }

        if (pick is { } tech)
        {
            AssignResearch(p, tech, field);
        }
        else if (fallBack)
        {
            FieldExhausted(p, field);
        }
    }

    private int? FavoredResearch(Player p, int field)
    {
        if (field is not (1 or 2))
        {
            return null;
        }

        return Open(p, field).FirstOrDefault(t => _favoredResearch.Contains(t.Id))?.Id;
    }

    /// <summary>The most expensive open tech, unless it costs more than half of them all together.</summary>
    private int? MostExpensiveResearch(Player p, int field)
    {
        if (field == ColonialField)
        {
            return null;
        }

        var total = 0;
        var best = -1;
        int? pick = null;
        foreach (var t in Open(p, field))
        {
            var cost = TechLevels.ResearchCost(t, p);
            total += cost;
            if (best < cost)
            {
                best = cost;
                pick = t.Id;
            }
        }

        return total / 2 < best ? null : pick;
    }

    /// <summary>
    /// The cheapest open tech, the first of equal cost. With
    /// <paramref name="colonialTies"/>, colonial ties go to the better resource.
    /// </summary>
    // and  for the tie rule
    private int? CheapestResearch(Player p, int field, bool colonialTies)
    {
        TechInfo? pick = null;
        var best = 0;
        foreach (var t in Open(p, field))
        {
            var cost = TechLevels.ResearchCost(t, p);
            if (pick is null || cost < best)
            {
                pick = t;
                best = cost;
            }
            else if (colonialTies && cost == best && field == ColonialField && _colonialRank[t.Resource] <= _colonialRank[pick.Resource])
            {
                pick = t;
            }
        }

        return pick?.Id;
    }

    /// <summary>Starts a tech in a field's slot, with any progress saved for it.</summary>
    private void AssignResearch(Player p, int tech, int field)
    {
        var slot = p.ResearchSlots[field];
        slot.Tech = tech;
        slot.Cost = TechLevels.ResearchCost(Tables.Tech(tech), p);
        slot.Progress = p.SavedProgress.GetValueOrDefault(tech);
    }

    /// <summary>
    /// A field with nothing left gives its allocation, a point at a time,
    /// to the other fields still researching.
    /// </summary>
    internal void FieldExhausted(Player p, int field)
    {
        if (Tables.Techs.Any(t => t.ResearchField == field && CanResearch(p, t)))
        {
            return;
        }

        var points = p.Research[field];
        p.Research[field] = 0;
        if (!Tables.Techs.Any(t => CanResearch(p, t)))
        {
            return;
        }

        WaitingFighter(p);
        while (points != 0)
        {
            var gave = false;
            for (var other = 0; other < ResearchFields && points != 0; other++)
            {
                if (p.ResearchSlots[other].Tech != ResearchSlot.None && other != field)
                {
                    points--;
                    p.Research[other]++;
                    gave = true;
                }
            }

            // The original loops for ever here when no other field is researching.
            if (!gave)
            {
                break;
            }
        }
    }

    /// <summary>The first unknown fighter goes in an empty space slot if it can be researched.</summary>
    private void WaitingFighter(Player p)
    {
        var fighter = Tables.Techs.FirstOrDefault(t => !p.Knows(t.Id) && t.Kind == TechInfo.FighterKind);
        if (fighter is not null && p.ResearchSlots[SpaceField].Tech == ResearchSlot.None && CanResearch(p, fighter))
        {
            AssignResearch(p, fighter.Id, SpaceField);
        }
    }
}
