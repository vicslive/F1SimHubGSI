using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace F1SimHubLive.Installer.Services;

public sealed class DeployOptions
{
    public required string SimHubInstallDir { get; init; }
    public required int DriverNumber { get; init; }
    public required string Source { get; init; } // "F1Live" or "MultiViewer"
    public string MultiViewerBaseUrl { get; init; } = "http://localhost:10101";
    public int MultiViewerPollMs { get; init; } = 250;
    public int MultiViewerTimingPollMs { get; init; } = 1000;
    public bool RestartSimHub { get; init; } = true;

    /// <summary>
    /// When true, the deployer flips every SimHub device's
    /// <c>CurrentIdleDashboard</c> to F1RaceSim_GSIFPEV2 (with a timestamped backup).
    /// When false, the deployer leaves the user's idle dashboard alone and the
    /// Done page surfaces a manual-setup warning.
    /// </summary>
    public bool SetIdleDashboard { get; init; } = true;
}

public sealed class Deployer
{
    public event Action<string>? Log;
    public event Action<int>? Progress;

    private readonly IdleDashboardService _idle = new();
    private readonly LedConfigRewireService _ledRewire = new();

    /// <summary>
    /// Per-device idle-dashboard changes recorded during the last deploy. Empty when
    /// <see cref="DeployOptions.SetIdleDashboard"/> is false.
    /// </summary>
    public List<IdleDashboardChange> LastIdleDashboardChanges { get; private set; } = new();

    /// <summary>
    /// Per-device LED-config plugin-name rewire results recorded during the last deploy.
    /// Empty list means no devices were scanned; entries with <c>Modified=false</c> and
    /// <c>OccurrencesReplaced=0</c> mean the device was already clean.
    /// </summary>
    public List<LedRewireChange> LastLedRewireChanges { get; private set; } = new();

    private void L(string msg) => Log?.Invoke(msg);
    private void P(int pct) => Progress?.Invoke(pct);

    public async Task DeployAsync(DeployOptions opts)
    {
        L($"Target SimHub directory: {opts.SimHubInstallDir}");

        await Task.Run(() => MaybeStopSimHub()).ConfigureAwait(false);
        P(10);

        var dashDir = Path.Combine(opts.SimHubInstallDir, "DashTemplates", "F1RaceSim_GSIFPEV2");
        Directory.CreateDirectory(dashDir);

        var pluginDest = Path.Combine(opts.SimHubInstallDir, "F1SimHubLive.dll");
        ReportExistingPluginVersion(pluginDest);

        L("Copying plugin DLLs...");
        ExtractResourceTo("F1SimHubLive.dll", pluginDest);
        ExtractResourceTo("Microsoft.AspNet.SignalR.Client.dll", Path.Combine(opts.SimHubInstallDir, "Microsoft.AspNet.SignalR.Client.dll"));
        ReportNewlyInstalledPluginVersion(pluginDest);
        P(40);

        L("Copying F1RaceSim_GSIFPEV2 dashboard files...");
        ExtractResourceTo("F1RaceSim_GSIFPEV2.djson", Path.Combine(dashDir, "F1RaceSim_GSIFPEV2.djson"));
        ExtractResourceTo("F1RaceSim_GSIFPEV2.djson.ressources", Path.Combine(dashDir, "F1RaceSim_GSIFPEV2.djson.ressources"));
        ExtractResourceTo("F1RaceSim_GSIFPEV2.djson.metadata", Path.Combine(dashDir, "F1RaceSim_GSIFPEV2.djson.metadata"));
        ExtractResourceTo("F1RaceSim_GSIFPEV2.djson.png", Path.Combine(dashDir, "F1RaceSim_GSIFPEV2.djson.png"));
        ExtractResourceTo("F1RaceSim_GSIFPEV2.djson.00.png", Path.Combine(dashDir, "F1RaceSim_GSIFPEV2.djson.00.png"));
        P(75);

        L($"Writing Settings.json for driver #{opts.DriverNumber} (source: {opts.Source})...");
        WriteSettings(opts);
        P(85);

        L("");
        L("Scanning per-device LED configurations for stale plugin-name references...");
        LastLedRewireChanges = _ledRewire.RewireEverywhere(opts.SimHubInstallDir, L);
        var rewiredDevices = 0;
        var rewiredTotal = 0;
        foreach (var c in LastLedRewireChanges)
        {
            if (!c.Modified) continue;
            rewiredDevices++;
            rewiredTotal += c.OccurrencesReplaced;
        }
        if (rewiredDevices > 0)
        {
            L($"LED config rewire: patched {rewiredTotal} reference(s) across {rewiredDevices} device(s).");
        }
        else
        {
            L("LED config rewire: no stale references found.");
        }
        P(90);

        if (opts.SetIdleDashboard)
        {
            L("");
            L("Setting F1RaceSim_GSIFPEV2 as the SimHub idle dashboard on every connected device...");
            LastIdleDashboardChanges = _idle.SetIdleDashboardEverywhere(
                opts.SimHubInstallDir,
                IdleDashboardService.TargetDashboardName,
                L);
        }
        else
        {
            L("");
            L("Idle-dashboard change skipped (user opted out).");
            L("You must open SimHub > Dash Studio > select your device > pick 'F1RaceSim_GSIFPEV2' as the idle dashboard for the dash to show up automatically.");
            LastIdleDashboardChanges = new List<IdleDashboardChange>();
        }
        P(95);

        if (opts.RestartSimHub)
        {
            L("Starting SimHub...");
            StartSimHub(opts.SimHubInstallDir);
        }
        P(100);
        L("Deployment complete.");
    }

    private void MaybeStopSimHub()
    {
        var procs = Process.GetProcessesByName("SimHubWPF");
        if (procs.Length == 0) return;
        L($"Stopping {procs.Length} running SimHub process(es)...");
        foreach (var p in procs)
        {
            try { p.CloseMainWindow(); } catch { }
        }
        Task.Delay(1500).Wait();
        foreach (var p in Process.GetProcessesByName("SimHubWPF"))
        {
            try { p.Kill(); p.WaitForExit(2000); } catch { }
        }
    }

    private void StartSimHub(string dir)
    {
        var exe = Path.Combine(dir, "SimHubWPF.exe");
        if (!File.Exists(exe)) { L("SimHubWPF.exe not found — skipping start."); return; }
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = dir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            L($"Failed to start SimHub: {ex.Message}");
        }
    }

    private void WriteSettings(DeployOptions opts)
    {
        var settings = new
        {
            DriverNumber = opts.DriverNumber,
            OutputHz = 60,
            RenderDelayMs = 0,
            Source = opts.Source,
            MultiViewerBaseUrl = opts.MultiViewerBaseUrl,
            MultiViewerPollMs = opts.MultiViewerPollMs,
            MultiViewerTimingPollMs = opts.MultiViewerTimingPollMs,
        };
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(opts.SimHubInstallDir, "F1SimHubLive.Settings.json"), json, new UTF8Encoding(false));
    }

    private void ReportExistingPluginVersion(string pluginPath)
    {
        if (!File.Exists(pluginPath))
        {
            L("No prior F1SimHubLive.dll found — fresh install.");
            return;
        }
        try
        {
            var fvi = FileVersionInfo.GetVersionInfo(pluginPath);
            var existing = string.IsNullOrWhiteSpace(fvi.FileVersion) ? "unknown" : fvi.FileVersion;
            L($"Existing F1SimHubLive.dll detected — version {existing}.");
        }
        catch (Exception ex)
        {
            L($"Could not read existing plugin version: {ex.Message}");
        }
    }

    private void ReportNewlyInstalledPluginVersion(string pluginPath)
    {
        try
        {
            var fvi = FileVersionInfo.GetVersionInfo(pluginPath);
            var fresh = string.IsNullOrWhiteSpace(fvi.FileVersion) ? "unknown" : fvi.FileVersion;
            L($"Installed F1SimHubLive.dll version {fresh}.");
        }
        catch
        {
            // Non-fatal.
        }
    }

    private static void ExtractResourceTo(string assetName, string destPath)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(assetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Embedded resource not found: {assetName}");
        using var src = asm.GetManifestResourceStream(resName)!;
        using var dst = File.Create(destPath);
        src.CopyTo(dst);
    }
}
