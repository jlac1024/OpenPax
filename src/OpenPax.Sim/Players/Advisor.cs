namespace OpenPax.Sim.Players;

/// <summary>
/// An advisor (docs/sim/advisors.md): four per player at the
/// start, one per field (0 research, 1 espionage, 2 construction, 3
/// shipyards), plus a governor (role 5) for each colonised star.
/// Portrait = (species picture + 1) * 1000 + a draw of 0 to 9.
/// </summary>
public sealed class Advisor(int id, int role, int portrait, double[] skills)
{
    public const int GovernorRole = 5;

    /// <summary>The star of an advisor that is not a governor.</summary>
    public const int NoStar = -1;

    public int Id { get; } = id;

    public int Role { get; private set; } = role;

    public int Portrait { get; } = portrait;

    /// <summary>Skill per field (+8): a governor's grow, other advisors keep their own field's.</summary>
    public double[] Skills { get; } = skills;

    /// <summary>The star a governor runs (+0x2C); <see cref="NoStar"/> for the others.</summary>
    public int Star { get; internal set; } = NoStar;

    /// <summary>The player whose spies turned this advisor (+0x40); while set, it works against its owner.</summary>
    public Player? TurnedBy { get; internal set; }

    public bool IsGovernor => Role == GovernorRole;

    /// <summary>
    /// What the advisor adds to a field: a governor's skill for it, or
    /// another's own field; negated when turned.
    /// </summary>
    public double Value(int field)
    {
        var v = IsGovernor || field == Role ? Skills[field] : 0.0;
        return TurnedBy is null ? v : v * -1.0;
    }

    /// <summary>A governor takes a field: its other skills go, and it leaves its star.</summary>
    internal void Promote(int role)
    {
        Role = role;
        for (var k = 0; k < Skills.Length; k++)
        {
            if (k != role)
            {
                Skills[k] = 0.0;
            }
        }

        Star = NoStar;
    }

    public void AddTo(StateHash hash)
    {
        hash.Add(Id).Add(Role).Add(Portrait).Add(Star).Add(TurnedBy?.Index ?? -1);
        foreach (var s in Skills)
        {
            hash.Add(s);
        }
    }
}
