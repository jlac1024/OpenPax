using OpenPax.Data.Tables;

namespace OpenPax.Ui;

/// <summary>
/// Where designed species are kept between games (the original's
/// <c>Data\SpecData</c>; the port's per-user folder). The host gives the file.
/// </summary>
public interface ICustomSpecies
{
    /// <summary>The species kept, oldest first; none when the file is missing or cannot be read.</summary>
    IReadOnlyList<RaceTemplate> Load();

    /// <summary>Writes the whole list in place of what was kept.</summary>
    void Save(IReadOnlyList<RaceTemplate> species);
}
