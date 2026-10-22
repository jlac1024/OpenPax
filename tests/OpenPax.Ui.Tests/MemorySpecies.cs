using OpenPax.Data.Tables;

namespace OpenPax.Ui.Tests;

/// <summary>Designed species kept in memory, counting the writes.</summary>
internal sealed class MemorySpecies : ICustomSpecies
{
    public List<RaceTemplate> Kept { get; } = [];

    public int Saves { get; private set; }

    public IReadOnlyList<RaceTemplate> Load() => [.. Kept];

    public void Save(IReadOnlyList<RaceTemplate> species)
    {
        Kept.Clear();
        Kept.AddRange(species);
        Saves++;
    }
}
