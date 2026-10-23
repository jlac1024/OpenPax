using System.Reflection;

namespace OpenPax.Host.Update;

/// <summary>The version of the running game, as the release bundle was built with it (tools/package.sh).</summary>
internal static class AppVersion
{
    /// <summary>The running version; 0.0.0-dev for a development build.</summary>
    public static ReleaseVersion Current { get; } = Read();

    /// <summary>
    /// For trying updates with a development build: OPENPAX_PRETEND_VERSION
    /// makes it check as that version (docs/updates.md). Null when unset.
    /// </summary>
    public static ReleaseVersion? ForTesting() =>
        Environment.GetEnvironmentVariable("OPENPAX_PRETEND_VERSION") is { Length: > 0 } text ? ReleaseVersion.Parse(text) : null;

    private static ReleaseVersion Read()
    {
        var text = typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return text is not null && ReleaseVersion.Parse(text) is { } version ? version : new ReleaseVersion(0, 0, 0, "dev");
    }
}
