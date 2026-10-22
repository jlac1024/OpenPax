namespace OpenPax.Sim.Players;

/// <summary>
/// The last 50 samples, one every 200 ticks, of ten empire statistics, for
/// the comparison graphs (player +0x808 to +0xF10). Nothing in play reads them.
/// </summary>
public sealed class EmpireHistory
{
    public const int Samples = 50;
    public const int Empty = -1;

    public const int Money = 0;
    public const int Colonies = 1;
    public const int FleetStrength = 2;
    public const int Population = 3;
    public const int Research = 4;
    public const int Construction = 5;
    public const int YardWork = 6;
    public const int Espionage = 7;
    public const int TechsKnown = 8;
    public const int Score = 9;

    private readonly int[][] _series = [.. Enumerable.Range(0, 10).Select(_ => Enumerable.Repeat(Empty, Samples).ToArray())];

    /// <summary>One sample as an original save holds it.</summary>
    internal void Restore(int statistic, int sample, int value) => _series[statistic][sample] = value;

    public IReadOnlyList<int> Series(int statistic)
    {
        return _series[statistic];
    }

    /// <summary>
    /// Adds a sample: into the first empty slot, or, when full, after
    /// shifting out the oldest. Quirk kept: a real -1 counts as empty.
    /// </summary>
    internal void Append(int statistic, int value)
    {
        var s = _series[statistic];
        if (s[Samples - 1] != Empty)
        {
            Array.Copy(s, 1, s, 0, Samples - 1);
            s[Samples - 1] = value;
            return;
        }

        s[Array.IndexOf(s, Empty)] = value;
    }

    public void AddTo(StateHash hash)
    {
        foreach (var s in _series)
        {
            foreach (var v in s)
            {
                hash.Add(v);
            }
        }
    }
}
