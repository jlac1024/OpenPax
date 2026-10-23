using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace OpenPax.Host.Update;

/// <summary>
/// OpenPax's releases on GitHub (docs/updates.md): which is the newest the
/// player has asked for, and its bundle for the running platform.
/// </summary>
internal static class ReleaseFeed
{
    public const string Repository = "jlac1024/OpenPax";
    public const string ListUrl = "https://api.github.com/repos/" + Repository + "/releases?per_page=30";

    /// <summary>
    /// The bundle's platform as tools/package.sh names it: win-x64,
    /// linux-x64 or osx-arm64. Made from the system and processor, since
    /// the runtime's own name can be a distribution's (ubuntu.26.04-x64).
    /// </summary>
    public static string Platform =>
        (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-"
        + RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

    /// <summary>The bundle's file name for a version, as tools/package.sh names it.</summary>
    public static string AssetName(ReleaseVersion version, string platform) =>
        $"OpenPax-{version}-{platform}" + (platform.StartsWith("win", StringComparison.Ordinal) ? ".zip" : ".tar.gz");

    /// <summary>The release list, as GitHub's API gives it.</summary>
    public static async Task<string> FetchAsync(HttpClient http, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ListUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await http.SendAsync(request, cancel).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// The newest published release above <paramref name="current"/> that has
    /// a bundle for <paramref name="platform"/> with a SHA-256; pre-releases
    /// only when <paramref name="preReleases"/>. Null when there is none.
    /// </summary>
    public static ReleaseInfo? Newest(string json, ReleaseVersion current, bool preReleases, string platform)
    {
        ReleaseInfo? best = null;
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (Bool(release, "draft") || (Bool(release, "prerelease") && !preReleases)
                || Text(release, "tag_name") is not { } tag || ReleaseVersion.Parse(tag) is not { } version
                || version <= current || (best is not null && version <= best.Version))
            {
                continue;
            }

            var name = AssetName(version, platform);
            if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var asset in assets.EnumerateArray())
            {
                if (Text(asset, "name") == name && Text(asset, "browser_download_url") is { } url
                    && Text(asset, "digest") is { } digest && digest.StartsWith("sha256:", StringComparison.Ordinal)
                    && asset.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes)
                    && url.StartsWith("https://github.com/" + Repository + "/releases/download/", StringComparison.Ordinal))
                {
                    best = new ReleaseInfo(version, Bool(release, "prerelease"), Text(release, "html_url") ?? "", name, url, bytes, digest["sha256:".Length..].ToLowerInvariant());
                }
            }
        }

        return best;
    }

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
