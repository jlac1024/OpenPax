namespace OpenPax.Data.Import;

/// <summary>One part of a longer job: its 0 to 1 shown as a slice of the whole's.</summary>
internal sealed class ScaledProgress(IProgress<ImportProgress>? whole, double start, double share) : IProgress<ImportProgress>
{
    public void Report(ImportProgress value) =>
        whole?.Report(value with { Fraction = start + (share * Math.Clamp(value.Fraction, 0, 1)) });
}
