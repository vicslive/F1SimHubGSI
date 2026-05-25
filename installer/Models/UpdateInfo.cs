namespace F1SimHubLive.Installer.Models;

/// <summary>
/// Result of checking GitHub Releases for a newer installer.
/// Returned only when a strictly-newer release exists.
/// </summary>
public sealed class UpdateInfo
{
    public required Version CurrentVersion { get; init; }
    public required Version LatestVersion { get; init; }
    public required string LatestTag { get; init; }
    public required string ReleasePageUrl { get; init; }
    public string? InstallerAssetUrl { get; init; }
    public string? ReleaseNotes { get; init; }
}
