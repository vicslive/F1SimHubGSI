using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using F1SimHubLive.Installer.Models;

namespace F1SimHubLive.Installer.Services;

/// <summary>
/// Checks the GitHub Releases API to see whether a newer F1SimHubLive Installer
/// is published. Designed to fail silently — a network outage, GitHub API rate
/// limit, or malformed response must never block the user from installing.
/// </summary>
public sealed class UpdateChecker
{
    // Public, unauthenticated GitHub API. 60 req/hr per IP is plenty for an
    // installer that runs once per user.
    private const string ReleasesApiUrl =
        "https://api.github.com/repos/vicslive/F1SimHubLive/releases/latest";

    private const string ReleasesPageFallback =
        "https://github.com/vicslive/F1SimHubLive/releases/latest";

    private readonly TimeSpan _timeout;

    public UpdateChecker(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(3);
    }

    /// <summary>
    /// Returns an <see cref="UpdateInfo"/> when GitHub reports a release whose
    /// version is strictly greater than the running installer's version.
    /// Returns null when up-to-date, offline, rate-limited, or anything else
    /// goes wrong. Never throws.
    /// </summary>
    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            var current = GetCurrentInstallerVersion();
            using var http = new HttpClient { Timeout = _timeout };
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("F1SimHubLive-Installer", current.ToString()));
            http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var resp = await http.GetAsync(ReleasesApiUrl, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagElem)) return null;
            var tag = tagElem.GetString();
            if (string.IsNullOrWhiteSpace(tag)) return null;

            if (!TryParseSemverTag(tag, out var latest)) return null;
            if (latest <= current) return null;

            var releasePageUrl = root.TryGetProperty("html_url", out var hu) && hu.ValueKind == JsonValueKind.String
                ? hu.GetString() ?? ReleasesPageFallback
                : ReleasesPageFallback;

            string? installerAssetUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("name", out var n) &&
                        n.ValueKind == JsonValueKind.String &&
                        n.GetString()?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true &&
                        asset.TryGetProperty("browser_download_url", out var url) &&
                        url.ValueKind == JsonValueKind.String)
                    {
                        installerAssetUrl = url.GetString();
                        break;
                    }
                }
            }

            string? notes = null;
            if (root.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String)
            {
                notes = b.GetString();
            }

            return new UpdateInfo
            {
                CurrentVersion = current,
                LatestVersion = latest,
                LatestTag = tag,
                ReleasePageUrl = releasePageUrl,
                InstallerAssetUrl = installerAssetUrl,
                ReleaseNotes = notes,
            };
        }
        catch
        {
            // Silent: network errors, JSON shape changes, timeouts — none of
            // these should ever stop the user from installing what they have.
            return null;
        }
    }

    /// <summary>
    /// URL to direct the user to if they choose "Download" on the update
    /// banner. Prefers a deep link to the .exe asset; falls back to the
    /// release page.
    /// </summary>
    public static string ResolveDownloadUrl(UpdateInfo info)
        => info.InstallerAssetUrl ?? info.ReleasePageUrl;

    /// <summary>
    /// Reads the running installer's own version from its assembly. CI sets
    /// these properties from the git tag at publish time; locally they fall
    /// back to whatever's hardcoded in the csproj.
    /// </summary>
    public static Version GetCurrentInstallerVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info) && TryParseSemverTag(info, out var v)) return v;

        var fv = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        if (!string.IsNullOrWhiteSpace(fv) && Version.TryParse(fv, out var fileVer)) return fileVer;

        return asm.GetName().Version ?? new Version(0, 0, 0, 0);
    }

    /// <summary>
    /// Parses tags like "v1.0.2", "1.0.2", "v1.0.2-rc1", "1.0.2+meta" into a
    /// <see cref="Version"/>. Strips a leading 'v', drops any "-prerelease"
    /// or "+metadata" suffix, and pads to 3 components so 1.0 compares
    /// sensibly against 1.0.0.
    /// </summary>
    private static bool TryParseSemverTag(string tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;

        var s = tag.Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);

        var dash = s.IndexOf('-');
        if (dash >= 0) s = s.Substring(0, dash);
        var plus = s.IndexOf('+');
        if (plus >= 0) s = s.Substring(0, plus);

        var parts = s.Split('.');
        if (parts.Length < 2) return false;
        if (parts.Length == 2) s = s + ".0";

        if (Version.TryParse(s, out var parsed) && parsed != null)
        {
            version = parsed;
            return true;
        }
        return false;
    }
}
