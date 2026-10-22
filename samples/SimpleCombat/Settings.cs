using OpenPax.Data.Toml;

namespace SimpleCombat;

/// <summary>The numbers from simple-combat.toml in the mod's folder (each is explained there); any missing take their defaults.</summary>
public sealed record Settings
{
    public int RoundEvery { get; init; } = 5;

    public double EngageDistance { get; init; } = 50;

    public double AttackShare { get; init; } = 0.25;

    public double ShortRangeVsSmallCraft { get; init; } = 0.5;

    public double DronesVsSmallCraft { get; init; } = 0.5;

    public double MostStopped { get; init; } = 0.8;

    public double OtherWeaponsOnPeople { get; init; } = 0.5;

    public double PopulationPerAttack { get; init; } = 0.02;

    public double CaptureAt { get; init; } = 0.5;

    public double MinesPerUnit { get; init; } = 75;

    public double MineStrikes { get; init; } = 0.02;

    public double SweptStrikes { get; init; } = 0.15;

    public double SweepRate { get; init; } = 0.05;

    public double MineDamageShare { get; init; } = 0.25;

    public int ExperiencePerRound { get; init; } = 1;

    public static Settings Default { get; } = new();

    public static Settings Read(string path)
    {
        if (!File.Exists(path))
        {
            return Default;
        }

        var t = TomlReader.Parse(File.ReadAllText(path));
        var d = Default;
        double N(string key, double fallback) => t.TryGetValue(key, out var v) ? v switch { long l => l, double x => x, _ => fallback } : fallback;
        return new Settings
        {
            RoundEvery = Math.Max(1, (int)N("round_every", d.RoundEvery)),
            EngageDistance = Math.Max(0, N("engage_distance", d.EngageDistance)),
            AttackShare = N("attack_share", d.AttackShare),
            ShortRangeVsSmallCraft = N("short_range_vs_small_craft", d.ShortRangeVsSmallCraft),
            DronesVsSmallCraft = N("drones_vs_small_craft", d.DronesVsSmallCraft),
            MostStopped = Math.Clamp(N("most_stopped", d.MostStopped), 0, 1),
            OtherWeaponsOnPeople = N("other_weapons_on_people", d.OtherWeaponsOnPeople),
            PopulationPerAttack = N("population_per_attack", d.PopulationPerAttack),
            CaptureAt = N("capture_at", d.CaptureAt),
            MinesPerUnit = N("mines_per_unit", d.MinesPerUnit),
            MineStrikes = N("mine_strikes", d.MineStrikes),
            SweptStrikes = N("swept_strikes", d.SweptStrikes),
            SweepRate = N("sweep_rate", d.SweepRate),
            MineDamageShare = N("mine_damage_share", d.MineDamageShare),
            ExperiencePerRound = Math.Max(0, (int)N("experience_per_round", d.ExperiencePerRound)),
        };
    }
}
