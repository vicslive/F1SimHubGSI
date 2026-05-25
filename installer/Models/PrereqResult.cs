using F1SimHubLive.Installer.Services;

namespace F1SimHubLive.Installer.Models;

public sealed class PrereqResult
{
    public bool SimHubInstalled { get; set; }
    public string? SimHubPath { get; set; }
    public string? SimHubVersion { get; set; }

    public bool MultiViewerInstalled { get; set; }
    public string? MultiViewerPath { get; set; }
    public string? MultiViewerVersion { get; set; }

    public bool MultiViewerRunning { get; set; }
    public bool F1SubscriptionActive { get; set; }
    public bool LiveTimingActive { get; set; }
    public string? MultiViewerApiMessage { get; set; }
    public string? LiveTimingSessionName { get; set; }

    /// <summary>
    /// SimHub Dash Studio devices the user has configured. Populated from
    /// <see cref="IdleDashboardService.EnumerateDevices(string)"/> during the
    /// prereq check so the Driver page doesn't have to re-enumerate.
    /// </summary>
    public List<SimHubDevice> Wheels { get; set; } = new();

    /// <summary>
    /// At least one SimHub device has an <c>LCD.display</c> section — i.e. a
    /// wheel/screen capable of showing a Dash Studio dashboard.
    /// </summary>
    public bool HasWheelWithLcd => Wheels.Any(w => w.HasLcdDisplaySection);

    public bool AllSatisfied =>
        SimHubInstalled && MultiViewerInstalled
        && (MultiViewerRunning ? (F1SubscriptionActive && LiveTimingActive) : true);

    public bool CanProceed => SimHubInstalled && MultiViewerInstalled;
}
