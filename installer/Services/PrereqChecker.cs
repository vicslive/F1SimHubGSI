using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using F1SimHubLive.Installer.Models;
using Microsoft.Win32;

namespace F1SimHubLive.Installer.Services;

public sealed class PrereqChecker
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly IdleDashboardService _idle = new();

    public async Task<PrereqResult> CheckAsync()
    {
        var r = new PrereqResult();
        DetectSimHub(r);
        DetectMultiViewer(r);
        await CheckMultiViewerApiAsync(r).ConfigureAwait(false);
        DetectWheels(r);
        return r;
    }

    private void DetectWheels(PrereqResult r)
    {
        if (!r.SimHubInstalled || string.IsNullOrEmpty(r.SimHubPath))
        {
            r.Wheels = new List<SimHubDevice>();
            return;
        }
        try
        {
            r.Wheels = _idle.EnumerateDevices(r.SimHubPath);
        }
        catch
        {
            r.Wheels = new List<SimHubDevice>();
        }
    }

    private static readonly string[] _simHubCandidates =
    {
        @"C:\Program Files (x86)\SimHub\SimHubWPF.exe",
        @"C:\Program Files\SimHub\SimHubWPF.exe",
    };

    public static string? FindSimHubInstallDir()
    {
        foreach (var exe in _simHubCandidates)
        {
            if (File.Exists(exe)) return Path.GetDirectoryName(exe);
        }

        // Registry uninstall keys
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var subkey in new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            })
            {
                using var key = hive.OpenSubKey(subkey);
                if (key == null) continue;
                foreach (var name in key.GetSubKeyNames())
                {
                    using var sub = key.OpenSubKey(name);
                    var dn = sub?.GetValue("DisplayName") as string;
                    var loc = sub?.GetValue("InstallLocation") as string;
                    if (dn != null && dn.StartsWith("SimHub", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(loc) && Directory.Exists(loc))
                    {
                        return loc;
                    }
                }
            }
        }
        return null;
    }

    private static void DetectSimHub(PrereqResult r)
    {
        var dir = FindSimHubInstallDir();
        if (dir == null) { r.SimHubInstalled = false; return; }

        var exe = Path.Combine(dir, "SimHubWPF.exe");
        if (!File.Exists(exe)) { r.SimHubInstalled = false; return; }

        r.SimHubInstalled = true;
        r.SimHubPath = dir;
        try
        {
            var vi = FileVersionInfo.GetVersionInfo(exe);
            r.SimHubVersion = vi.FileVersion;
        }
        catch { /* ignore */ }
    }

    private static readonly string[] _mvCandidates =
    {
        @"%LOCALAPPDATA%\Programs\F1MV\F1MV.exe",
        @"%LOCALAPPDATA%\Programs\f1-multi-viewer\F1MV.exe",
        @"%LOCALAPPDATA%\Programs\f1mv\F1MV.exe",
        @"C:\Program Files\F1MV\F1MV.exe",
        @"C:\Program Files (x86)\F1MV\F1MV.exe",
    };

    public static string? FindMultiViewerInstallDir()
    {
        foreach (var rel in _mvCandidates)
        {
            var exe = Environment.ExpandEnvironmentVariables(rel);
            if (File.Exists(exe)) return Path.GetDirectoryName(exe);
        }

        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var subkey in new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            })
            {
                using var key = hive.OpenSubKey(subkey);
                if (key == null) continue;
                foreach (var name in key.GetSubKeyNames())
                {
                    using var sub = key.OpenSubKey(name);
                    var dn = sub?.GetValue("DisplayName") as string;
                    var loc = sub?.GetValue("InstallLocation") as string;
                    if (dn != null && (dn.Contains("MultiViewer", StringComparison.OrdinalIgnoreCase)
                                       || dn.Contains("F1MV", StringComparison.OrdinalIgnoreCase))
                        && !string.IsNullOrWhiteSpace(loc) && Directory.Exists(loc))
                    {
                        return loc;
                    }
                }
            }
        }
        return null;
    }

    private static void DetectMultiViewer(PrereqResult r)
    {
        var dir = FindMultiViewerInstallDir();
        if (dir == null) { r.MultiViewerInstalled = false; return; }
        r.MultiViewerInstalled = true;
        r.MultiViewerPath = dir;
        try
        {
            var exe = Directory.EnumerateFiles(dir, "F1MV.exe", SearchOption.TopDirectoryOnly).FirstOrDefault()
                      ?? Directory.EnumerateFiles(dir, "MultiViewer.exe", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (exe != null)
            {
                var vi = FileVersionInfo.GetVersionInfo(exe);
                r.MultiViewerVersion = vi.FileVersion;
            }
        }
        catch { /* ignore */ }
    }

    private static async Task CheckMultiViewerApiAsync(PrereqResult r)
    {
        // Step 1: confirm the MultiViewer local API is up at all.
        try
        {
            var hb = await _http.GetAsync("http://localhost:10101/api/v1/live-timing/Heartbeat").ConfigureAwait(false);
            r.MultiViewerRunning = true;
            r.F1SubscriptionActive = hb.IsSuccessStatusCode;
            if (!hb.IsSuccessStatusCode)
            {
                r.MultiViewerApiMessage =
                    $"MultiViewer API responded {(int)hb.StatusCode} — sign in to F1 TV inside MultiViewer.";
                return;
            }
        }
        catch (Exception ex)
        {
            r.MultiViewerRunning = false;
            r.MultiViewerApiMessage =
                $"Not reachable on :10101 — start MultiViewer and sign in to F1 TV. ({ex.GetType().Name})";
            return;
        }

        // Step 2: confirm a Live Timing session is *actively running* inside MultiViewer.
        //
        // Heartbeat alone returns 200 as soon as MultiViewer is running and signed in —
        // even if the user is only watching the broadcast video feed. Telemetry only
        // flows when the user has opened the **Live Timing** view (for a live session
        // or, in replay mode, after pressing "Replay Live Timing"). SessionInfo returns
        // populated JSON only in that case; it is empty/404 when MultiViewer is up but
        // no Live Timing context is loaded.
        try
        {
            var si = await _http.GetAsync("http://localhost:10101/api/v1/live-timing/SessionInfo")
                .ConfigureAwait(false);
            if (!si.IsSuccessStatusCode)
            {
                r.LiveTimingActive = false;
                r.MultiViewerApiMessage =
                    "MultiViewer is running and signed in, but Live Timing is not active. " +
                    "Open the session and launch the Live Timing view (for a replay, click " +
                    "\"Replay Live Timing\"). Just watching the video feed is not enough.";
                return;
            }

            var body = await si.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body) || body.Length < 10)
            {
                r.LiveTimingActive = false;
                r.MultiViewerApiMessage =
                    "MultiViewer is running and signed in, but Live Timing has no data yet. " +
                    "Open the session and launch the Live Timing view (for a replay, click " +
                    "\"Replay Live Timing\"). Just watching the video feed is not enough.";
                return;
            }

            r.LiveTimingActive = true;
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("Meeting", out var meeting)
                    && meeting.TryGetProperty("Name", out var mname))
                {
                    r.LiveTimingSessionName = mname.GetString();
                }
                else if (doc.RootElement.TryGetProperty("Name", out var name))
                {
                    r.LiveTimingSessionName = name.GetString();
                }
            }
            catch { /* shape is best-effort; success was the body check above */ }

            r.MultiViewerApiMessage = r.LiveTimingSessionName != null
                ? $"Live Timing active — {r.LiveTimingSessionName}."
                : "Live Timing active — telemetry is flowing.";
        }
        catch (Exception ex)
        {
            r.LiveTimingActive = false;
            r.MultiViewerApiMessage =
                $"MultiViewer API up, but SessionInfo probe failed ({ex.GetType().Name}). " +
                "Open the Live Timing view in MultiViewer (for a replay, click \"Replay Live Timing\").";
        }
    }
}
