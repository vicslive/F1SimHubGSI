using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using F1SimHubLive.Installer.Models;
using F1SimHubLive.Installer.Services;

namespace F1SimHubLive.Installer;

public partial class MainWindow : Window
{
    private enum Page { Welcome, Prereqs, Driver, Deploy, Done }
    private Page _page = Page.Welcome;

    private readonly PrereqChecker _prereq = new();
    private readonly DriverListService _drivers = new();
    private readonly Deployer _deployer = new();
    private readonly UpdateChecker _updateChecker = new();
    private readonly IdleDashboardService _idleService = new();

    private PrereqResult? _prereqResult;
    private List<F1Driver> _driverList = new();
    private UpdateInfo? _pendingUpdate;
    private List<SimHubDevice> _devices = new();

    public MainWindow()
    {
        InitializeComponent();
        _deployer.Log += msg => Dispatcher.Invoke(() => AppendLog(msg));
        _deployer.Progress += pct => Dispatcher.Invoke(() => DeployProgress.Value = pct);
        ApplyVersionLabels();
        Loaded += MainWindow_Loaded;
    }

    private void ApplyVersionLabels()
    {
        var v = UpdateChecker.GetCurrentInstallerVersion();
        var info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? v.ToString(3);
        var plus = info.IndexOf('+');
        if (plus >= 0) info = info.Substring(0, plus);
        VersionLabel.Text = "v" + info;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Fire-and-forget update check. Silent on failure — never blocks install.
        try
        {
            var info = await _updateChecker.CheckAsync().ConfigureAwait(true);
            if (info != null) ShowUpdateBanner(info);
        }
        catch
        {
            // UpdateChecker already swallows its own errors; this is belt-and-braces.
        }
    }

    private void ShowUpdateBanner(UpdateInfo info)
    {
        _pendingUpdate = info;
        UpdateBannerTitle.Text =
            $"A newer installer is available — {info.LatestTag} (you have v{info.CurrentVersion.ToString(3)})";
        UpdateBannerSubtitle.Text =
            "Click \"Download\" to open the latest release in your browser. " +
            "You can also continue with the current installer if you prefer.";
        UpdateBanner.Visibility = Visibility.Visible;
    }

    private void BtnUpdateDownload_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate == null) return;

        // Defense-in-depth: UpdateChecker.ResolveDownloadUrl already validates,
        // but we re-check at the last mile before ShellExecute. The installer
        // runs elevated (app.manifest requestedExecutionLevel=requireAdministrator),
        // so anything Process.Start launches inherits that token.
        var url = UpdateChecker.ResolveDownloadUrl(_pendingUpdate);
        if (!UpdateChecker.IsTrustedGitHubUrl(url))
        {
            MessageBox.Show(
                "The update URL did not pass the trusted-host check and was not opened.\n\n" +
                "Visit the releases page manually: https://github.com/vicslive/F1SimHubLive/releases/latest",
                "F1SimHubLive Installer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not open the download page in your browser.\n\n{ex.Message}\n\n" +
                $"Visit: {_pendingUpdate.ReleasePageUrl}",
                "F1SimHubLive Installer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnUpdateDismiss_Click(object sender, RoutedEventArgs e)
    {
        UpdateBanner.Visibility = Visibility.Collapsed;
    }

    private void AppendLog(string msg)
    {
        DeployLog.AppendText(msg + Environment.NewLine);
        DeployLog.ScrollToEnd();
    }

    private void ShowPage(Page p)
    {
        _page = p;
        PageWelcome.Visibility = p == Page.Welcome ? Visibility.Visible : Visibility.Collapsed;
        PagePrereqs.Visibility = p == Page.Prereqs ? Visibility.Visible : Visibility.Collapsed;
        PageDriver.Visibility = p == Page.Driver ? Visibility.Visible : Visibility.Collapsed;
        PageDeploy.Visibility = p == Page.Deploy ? Visibility.Visible : Visibility.Collapsed;
        PageDone.Visibility = p == Page.Done ? Visibility.Visible : Visibility.Collapsed;

        BtnBack.IsEnabled = p is Page.Prereqs or Page.Driver;
        BtnNext.Content = p switch
        {
            Page.Welcome => "Next",
            Page.Prereqs => "Next",
            Page.Driver => "Install",
            Page.Deploy => "Finish",
            Page.Done => "Close",
            _ => "Next",
        };
        BtnCancel.Visibility = p == Page.Done ? Visibility.Collapsed : Visibility.Visible;

        StepIndicator.Text = p switch
        {
            Page.Welcome => "Step 1 of 4 — Welcome",
            Page.Prereqs => "Step 2 of 4 — Prerequisites",
            Page.Driver => "Step 3 of 4 — Driver & source",
            Page.Deploy => "Step 4 of 4 — Install",
            Page.Done => "Done",
            _ => "",
        };
    }

    private async void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        switch (_page)
        {
            case Page.Welcome:
                ShowPage(Page.Prereqs);
                await RunPrereqsAsync();
                break;
            case Page.Prereqs:
                if (_prereqResult is null || !_prereqResult.CanProceed)
                {
                    MessageBox.Show(this,
                        "SimHub and F1 MultiViewer must both be installed before continuing.",
                        "Missing prerequisite", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                ShowPage(Page.Driver);
                await LoadDriversAsync();
                LoadIdleDashboardState();
                break;
            case Page.Driver:
                if (CmbDriver.SelectedItem is not F1Driver d)
                {
                    MessageBox.Show(this, "Please pick a driver.", "Missing driver",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                ShowPage(Page.Deploy);
                await DeployAsync(d);
                break;
            case Page.Deploy:
                ShowPage(Page.Done);
                break;
            case Page.Done:
                Close();
                break;
        }
    }

    private async void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        switch (_page)
        {
            case Page.Prereqs:
                ShowPage(Page.Welcome);
                break;
            case Page.Driver:
                ShowPage(Page.Prereqs);
                await RunPrereqsAsync();
                break;
        }
    }

    private void LoadIdleDashboardState()
    {
        // Devices are already enumerated during the prereq check (RenderWheelTile).
        // Just render the consent panel here.
        if (_devices.Count == 0)
        {
            IdleDashCurrentLabel.Text =
                "No SimHub Dash Studio devices detected. The dashboard files will still be copied — " +
                "if you connect a screen later, set its idle dashboard to F1RaceSim_GSIFPEV2 manually.";
            return;
        }

        var lines = new List<string>();
        var allAlreadySet = true;
        foreach (var d in _devices)
        {
            var current = string.IsNullOrEmpty(d.CurrentIdleDashboard) ? "(unset)" : d.CurrentIdleDashboard;
            var already = string.Equals(d.CurrentIdleDashboard, IdleDashboardService.TargetDashboardName, StringComparison.Ordinal);
            if (!already) allAlreadySet = false;
            var marker = already ? " ✓ already F1RaceSim_GSIFPEV2" : "";
            lines.Add($"• {d.DisplayName}: currently '{current}'{marker}");
        }

        var header = allAlreadySet
            ? "All your SimHub devices are already showing F1RaceSim_GSIFPEV2 — checking the box re-confirms it."
            : "We will change the idle dashboard on these SimHub devices:";

        IdleDashCurrentLabel.Text = header + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

    private async void BtnRecheck_Click(object sender, RoutedEventArgs e) => await RunPrereqsAsync();

    private async Task RunPrereqsAsync()
    {
        SimHubIcon.Text = MVIcon.Text = MVApiIcon.Text = WheelIcon.Text = "⏳";
        SimHubStatus.Text = MVStatus.Text = MVApiStatus.Text = WheelStatus.Text = "Checking...";
        SimHubStatus.Foreground = MVStatus.Foreground = MVApiStatus.Foreground = WheelStatus.Foreground =
            System.Windows.Media.Brushes.LightGray;
        PrereqHint.Text = "";
        BtnNext.IsEnabled = false;

        try
        {
            _prereqResult = await _prereq.CheckAsync();
        }
        catch (Exception ex)
        {
            PrereqHint.Text = $"Check failed: {ex.Message}";
            BtnNext.IsEnabled = false;
            return;
        }

        var ok = (string)"✓";
        var bad = (string)"✗";
        var warn = (string)"⚠";

        if (_prereqResult.SimHubInstalled)
        {
            SimHubIcon.Text = ok;
            SimHubIcon.Foreground = System.Windows.Media.Brushes.LightGreen;
            SimHubStatus.Text = $"Installed at {_prereqResult.SimHubPath} (v{_prereqResult.SimHubVersion ?? "?"})";
        }
        else
        {
            SimHubIcon.Text = bad;
            SimHubIcon.Foreground = System.Windows.Media.Brushes.Salmon;
            SimHubStatus.Text = "Not found. Install SimHub from https://www.simhubdash.com/";
        }

        if (_prereqResult.MultiViewerInstalled)
        {
            MVIcon.Text = ok;
            MVIcon.Foreground = System.Windows.Media.Brushes.LightGreen;
            MVStatus.Text = $"Installed at {_prereqResult.MultiViewerPath}" +
                            (_prereqResult.MultiViewerVersion != null ? $" (v{_prereqResult.MultiViewerVersion})" : "");
        }
        else
        {
            MVIcon.Text = bad;
            MVIcon.Foreground = System.Windows.Media.Brushes.Salmon;
            MVStatus.Text = "Not found. Install F1 MultiViewer from https://multiviewer.app/";
        }

        if (_prereqResult.MultiViewerRunning && _prereqResult.F1SubscriptionActive && _prereqResult.LiveTimingActive)
        {
            MVApiIcon.Text = ok;
            MVApiIcon.Foreground = System.Windows.Media.Brushes.LightGreen;
            MVApiStatus.Text = _prereqResult.MultiViewerApiMessage ?? "Live Timing active.";
        }
        else if (_prereqResult.MultiViewerRunning && _prereqResult.F1SubscriptionActive)
        {
            MVApiIcon.Text = warn;
            MVApiIcon.Foreground = System.Windows.Media.Brushes.Gold;
            MVApiStatus.Text = _prereqResult.MultiViewerApiMessage
                ?? "MultiViewer is running and signed in, but Live Timing is not active. "
                + "Open the session and launch the Live Timing view (for a replay, click \"Replay Live Timing\"). "
                + "Just watching the video feed is not enough.";
        }
        else if (_prereqResult.MultiViewerRunning)
        {
            MVApiIcon.Text = warn;
            MVApiIcon.Foreground = System.Windows.Media.Brushes.Gold;
            MVApiStatus.Text = _prereqResult.MultiViewerApiMessage ?? "Running, but no F1 TV subscription detected. Sign in to F1 TV inside MultiViewer.";
        }
        else
        {
            MVApiIcon.Text = warn;
            MVApiIcon.Foreground = System.Windows.Media.Brushes.Gold;
            MVApiStatus.Text = _prereqResult.MultiViewerApiMessage ?? "Not running. You can still install — just start MultiViewer and open Live Timing before using the dashboard.";
        }

        BtnNext.IsEnabled = _prereqResult.CanProceed;
        RenderWheelTile(_prereqResult);
        if (!_prereqResult.CanProceed)
        {
            PrereqHint.Text = "Install the missing components above, then click Re-check.";
        }
        else if (!_prereqResult.MultiViewerRunning || !_prereqResult.F1SubscriptionActive || !_prereqResult.LiveTimingActive)
        {
            PrereqHint.Text = "MultiViewer Live Timing is not currently streaming telemetry, but you can still install. "
                + "After install, open MultiViewer → sign in to F1 TV → load your session → click \"Replay Live Timing\" "
                + "(for replays) before launching the dashboard.";
        }
    }

    private void RenderWheelTile(PrereqResult r)
    {
        // Cache the device list for the Driver page so we don't re-enumerate.
        _devices = r.Wheels;

        var ok = "✓";
        var warn = "⚠";
        var lcd = r.Wheels.Where(w => w.HasLcdDisplaySection).ToList();
        var nonLcd = r.Wheels.Count - lcd.Count;

        if (!r.SimHubInstalled)
        {
            WheelIcon.Text = warn;
            WheelIcon.Foreground = System.Windows.Media.Brushes.Gold;
            WheelStatus.Text = "Will be checked once SimHub is installed.";
            return;
        }

        if (lcd.Count == 0)
        {
            WheelIcon.Text = warn;
            WheelIcon.Foreground = System.Windows.Media.Brushes.Gold;
            WheelStatus.Text = nonLcd > 0
                ? $"No LCD-capable devices found in SimHub Devices ({nonLcd} device(s) without a screen). "
                  + "F1SimHubLive will still install — connect a screen-equipped wheel later and SimHub will pick it up."
                : "No devices found in SimHub Devices. F1SimHubLive will still install — "
                  + "connect your wheel, open SimHub once so it registers the device, then re-run this installer.";
            return;
        }

        WheelIcon.Text = ok;
        WheelIcon.Foreground = System.Windows.Media.Brushes.LightGreen;
        var names = lcd.Select(w =>
        {
            var label = w.DisplayName;
            if (!string.IsNullOrWhiteSpace(w.DeviceTypeName) && w.DeviceTypeName != label)
            {
                label = $"{label} ({w.DeviceTypeName})";
            }
            return label;
        });
        WheelStatus.Text = $"Detected: {string.Join(", ", names)}";
    }

    private async Task LoadDriversAsync()
    {
        CmbDriver.IsEnabled = false;
        DriverSourceLabel.Text = "Loading driver list...";
        var (drivers, source) = await _drivers.GetDriversAsync();
        _driverList = drivers;
        CmbDriver.ItemsSource = _driverList;
        DriverSourceLabel.Text = source;

        var ham = _driverList.FirstOrDefault(d => d.Number == 44);
        CmbDriver.SelectedItem = ham ?? _driverList.FirstOrDefault();
        CmbDriver.IsEnabled = true;
    }

    private async Task DeployAsync(F1Driver driver)
    {
        BtnNext.IsEnabled = false;
        BtnBack.IsEnabled = false;
        DeployLog.Clear();
        DeployProgress.Value = 0;

        var source = RbF1Live.IsChecked == true ? "F1Live" : "MultiViewer";
        var mvUrl = string.IsNullOrWhiteSpace(TxtMvUrl.Text) ? "http://localhost:10101" : TxtMvUrl.Text.Trim();
        var mvPoll = int.TryParse(TxtMvPoll.Text, out var p1) ? p1 : 250;
        var mvTPoll = int.TryParse(TxtMvTimingPoll.Text, out var p2) ? p2 : 1000;
        var setIdle = ChkSetIdleDash.IsChecked == true;

        DeploySummary.Text =
            $"Installing F1SimHubLive for driver #{driver.Number} {driver.FirstName} {driver.LastName} ({driver.Team}). Source: {source}.";

        var simHubDir = _prereqResult?.SimHubPath ?? @"C:\Program Files (x86)\SimHub";

        try
        {
            await _deployer.DeployAsync(new DeployOptions
            {
                SimHubInstallDir = simHubDir,
                DriverNumber = driver.Number,
                Source = source,
                MultiViewerBaseUrl = mvUrl,
                MultiViewerPollMs = mvPoll,
                MultiViewerTimingPollMs = mvTPoll,
                RestartSimHub = true,
                SetIdleDashboard = setIdle,
            });
            UpdateDoneIdleDashBanners(setIdle);
            BtnNext.IsEnabled = true;
        }
        catch (Exception ex)
        {
            AppendLog("");
            AppendLog("✗ Deployment failed:");
            AppendLog(ex.ToString());
            BtnBack.IsEnabled = true;
        }
    }

    private void UpdateDoneIdleDashBanners(bool userOptedIn)
    {
        if (!userOptedIn)
        {
            IdleDashConfirm.Visibility = Visibility.Collapsed;
            IdleDashWarning.Visibility = Visibility.Visible;
            return;
        }

        IdleDashWarning.Visibility = Visibility.Collapsed;

        var changes = _deployer.LastIdleDashboardChanges;
        if (changes.Count == 0)
        {
            // No SimHub devices found — nothing to confirm AND nothing to warn about.
            IdleDashConfirm.Visibility = Visibility.Collapsed;
            return;
        }

        var modifiedCount = changes.Count(c => c.Modified);
        var alreadyCount = changes.Count(c => !c.Modified && string.IsNullOrEmpty(c.Error));
        var failedCount = changes.Count(c => !string.IsNullOrEmpty(c.Error));

        IdleDashConfirmDetail.Text =
            $"Idle dashboard set to F1RaceSim_GSIFPEV2 on {modifiedCount} device(s)" +
            (alreadyCount > 0 ? $", already set on {alreadyCount}" : "") +
            (failedCount > 0 ? $", failed on {failedCount}" : "") +
            ". When SimHub starts and no game is running, your wheel will show the F1 Live dashboard automatically.";
        IdleDashConfirm.Visibility = Visibility.Visible;
    }
}
