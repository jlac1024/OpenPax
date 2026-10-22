namespace OpenPax.Data.Import;

/// <summary>How far an import has got: 0 to 1, and what it is doing now.</summary>
public readonly record struct ImportProgress(double Fraction, string Step);
