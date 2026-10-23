using System.Globalization;

namespace OpenPax.Host.Update;

/// <summary>
/// A release's version as its tag gives it (v0.1.2, or v0.2.0-beta.1): three
/// numbers, then an optional pre-release label. A version with a label sorts
/// before the same numbers without one, as semantic versioning has it.
/// </summary>
internal readonly record struct ReleaseVersion(int Major, int Minor, int Patch, string Label) : IComparable<ReleaseVersion>
{
    public bool IsDevelopment => Label == "dev";

    /// <summary>Reads "0.1.2", "v0.1.2" or "0.2.0-beta.1"; null for anything else.</summary>
    public static ReleaseVersion? Parse(string text)
    {
        var s = text.Trim();
        if (s.StartsWith('v') || s.StartsWith('V'))
        {
            s = s[1..];
        }

        // Build metadata (+commit) is not part of the order.
        var plus = s.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            s = s[..plus];
        }

        var dash = s.IndexOf('-', StringComparison.Ordinal);
        var label = dash >= 0 ? s[(dash + 1)..] : "";
        var numbers = (dash >= 0 ? s[..dash] : s).Split('.');
        if (numbers.Length != 3 || (dash >= 0 && label.Length == 0))
        {
            return null;
        }

        var parsed = new int[3];
        for (var i = 0; i < 3; i++)
        {
            if (!int.TryParse(numbers[i], NumberStyles.None, CultureInfo.InvariantCulture, out parsed[i]))
            {
                return null;
            }
        }

        return new ReleaseVersion(parsed[0], parsed[1], parsed[2], label);
    }

    public int CompareTo(ReleaseVersion other)
    {
        var numbers = (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));
        if (numbers != 0)
        {
            return numbers;
        }

        // Of the same numbers, the one without a label is the release proper.
        return (Label.Length == 0, other.Label.Length == 0) switch
        {
            (true, true) => 0,
            (true, false) => 1,
            (false, true) => -1,
            _ => CompareLabels(Label, other.Label),
        };
    }

    // Dot-separated parts: numbers compare as numbers and sort before words.
    private static int CompareLabels(string a, string b)
    {
        var (x, y) = (a.Split('.'), b.Split('.'));
        for (var i = 0; i < Math.Min(x.Length, y.Length); i++)
        {
            var xNumber = int.TryParse(x[i], NumberStyles.None, CultureInfo.InvariantCulture, out var xn);
            var yNumber = int.TryParse(y[i], NumberStyles.None, CultureInfo.InvariantCulture, out var yn);
            var c = (xNumber, yNumber) switch
            {
                (true, true) => xn.CompareTo(yn),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(x[i], y[i]),
            };
            if (c != 0)
            {
                return c;
            }
        }

        return x.Length.CompareTo(y.Length);
    }

    public static bool operator <(ReleaseVersion a, ReleaseVersion b) => a.CompareTo(b) < 0;

    public static bool operator >(ReleaseVersion a, ReleaseVersion b) => a.CompareTo(b) > 0;

    public static bool operator <=(ReleaseVersion a, ReleaseVersion b) => a.CompareTo(b) <= 0;

    public static bool operator >=(ReleaseVersion a, ReleaseVersion b) => a.CompareTo(b) >= 0;

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}") + (Label.Length > 0 ? "-" + Label : "");
}
