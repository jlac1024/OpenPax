namespace OpenPax.Data.Import;

/// <summary>The installer unpacker's plain 0 to 1, as the first slice of the import's progress.</summary>
internal sealed class UnpackProgress(IProgress<ImportProgress>? whole, double share) : IProgress<double>
{
    public void Report(double value) => whole?.Report(new ImportProgress(share * value, Strings.ImportUnpacking));
}
