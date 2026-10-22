using OpenPax.Data.Tables;
using OpenPax.Ui;

namespace OpenPax.Host;

/// <summary>
/// Designed species in the per-user folder, in the original's
/// <c>SpecData</c> layout, so an old file can simply be copied in. Written
/// to a temporary file and moved into place, so a failed write never loses
/// what was kept.
/// </summary>
internal sealed class SpeciesFile(string path, string defaultName, string defaultDescription, TextWriter log) : ICustomSpecies
{
    public const string FileName = "SpecData";

    public IReadOnlyList<RaceTemplate> Load()
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return RaceTemplate.ReadSpeciesFile(File.ReadAllBytes(path), defaultName, defaultDescription);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or OpenPax.Data.Resources.ResourceFormatException)
        {
            log.WriteLine(string.Format(Strings.SpeciesUnreadable, path, e.Message));
            return [];
        }
    }

    public void Save(IReadOnlyList<RaceTemplate> species)
    {
        try
        {
            var temp = path + ".new";
            File.WriteAllBytes(temp, RaceTemplate.WriteSpeciesFile(species));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log.WriteLine(string.Format(Strings.SpeciesUnwritable, path, e.Message));
        }
    }
}
