namespace OpenPax.Data.Install;

/// <summary>Outcome of checking a game folder against the GOG build.</summary>
public sealed record VerifyResult(string Folder, IReadOnlyList<string> Missing, IReadOnlyList<string> Different)
{
    public bool IsGogBuild => Missing.Count == 0 && Different.Count == 0;

    /// <summary>A plain message for the person running the import.</summary>
    public string Describe()
    {
        if (IsGogBuild)
        {
            return string.Format(Strings.VerifyOk, Folder);
        }

        var lines = new List<string> { string.Format(Strings.VerifyFailed, Folder) };
        lines.AddRange(Missing.Take(10).Select(p => string.Format(Strings.VerifyMissing, p)));
        lines.AddRange(Different.Take(10).Select(p => string.Format(Strings.VerifyDifferent, p)));
        var more = Missing.Count + Different.Count - Math.Min(Missing.Count, 10) - Math.Min(Different.Count, 10);
        if (more > 0)
        {
            lines.Add(string.Format(Strings.VerifyMore, more));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
