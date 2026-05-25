# F1SimHubLive

**SimHub plugin + custom Dash Studio dashboard that pipes live Formula 1 telemetry from F1's broadcast feed (or MultiViewer replay) onto a SimHub-connected wheel screen.**

The current ``F1RaceSim_GSIFPEV2`` dashboard is laid out for an 800×480 wheel screen and has been validated on the [GSI Formula Pro Elite V2](https://gomezsimindustries.com/products/formula-pro-elite-v2) and [GSI Hyper P1](https://gomezsimindustries.com/products/hyper-p1). Any other SimHub-LCD-capable wheel at the same resolution should also work; resolutions other than 800×480 will crop or scale.

You pick a driver number (`44` = Hamilton, `1` = Verstappen, `16` = Leclerc, …). The plugin pulls that driver's RPM, gear, speed, throttle, brake, DRS, lap time, sector splits, gap to leader, tyre compound, pit stops, weather, track status and race-control flags. The companion `F1RaceSim_GSIFPEV2` dashboard renders all of it as a broadcast-style dash with shift lights driven by the live RPM.

This is a fan tool for use during F1 broadcasts on F1 TV / official live timing.

```
F1 TV broadcast (~1–3s behind live)
        │
        ▼
[ livetiming.formula1.com SignalR ]            [ F1 MultiViewer local HTTP ]
        │                                              │
        └──────────────┬───────────────────────────────┘
                       ▼
              F1SimHubLive plugin (this repo)
                       │ (60 Hz interpolated render of ~3–10 Hz feed)
                       ▼
              SimHub property tree
                       │
                       ▼
              F1RaceSim_GSIFPEV2 Dash Studio dashboard
                       │
                       ▼
          Your SimHub-connected wheel screen + LEDs
```

---

## Table of contents

1. [Quick install (installer)](#quick-install-installer)
2. [Fresh-machine setup (first-time GSI wheel)](#fresh-machine-setup-first-time-gsi-wheel)
3. [What it does](#what-it-does)
4. [Architecture](#architecture)
5. [Two data sources: F1 Live vs MultiViewer](#two-data-sources-f1-live-vs-multiviewer)
6. [SimHub property reference](#simhub-property-reference)
7. [F1RaceSim_GSIFPEV2 dashboard](#F1RaceSim_GSIFPEV2-dashboard)
8. [Build the plugin](#build-the-plugin)
9. [Install (manual)](#install-manual)
10. [Configure](#configure)
11. [Run a session](#run-a-session)
12. [Troubleshooting](#troubleshooting)
13. [File layout](#file-layout)
14. [Known limitations](#known-limitations)
15. [License](#license)
16. [Companion docs](#companion-docs)
17. [Contributing](#contributing)

---

## Quick install (installer)

The easiest way to deploy F1SimHubLive to a new machine (for example, your media-room PC where you watch F1 TV via MultiViewer):

1. Download the latest `F1SimHubLive-Installer.exe` from the [Releases](https://github.com/vicslive/F1SimHubLive/releases) page.
2. **Prerequisites on the target machine** (install in this exact order if the wheel has never been connected to this PC before — see [Fresh-machine setup](#fresh-machine-setup-first-time-gsi-wheel) below):
   - [SimHub](https://www.simhubdash.com/) installed.
   - [F1 MultiViewer](https://multiviewer.app/) installed and signed in with an active [F1 TV](https://f1tv.formula1.com/) subscription.
   - **A Live Timing session running inside MultiViewer** — for a replay, after loading the session you must click **"Replay Live Timing"** so the local API at `http://localhost:10101` actually emits telemetry. Watching only the video feed is *not* enough; the prereq probe and the plugin both pull from the Live Timing data stream, which is only active in that view.
   - **GSI SimOS** installed (the wheel's vendor companion — install BEFORE plugging in the wheel for the first time).
   - Your GSI wheel connected via USB and visible in SimHub *Devices*.
3. Right-click the .exe → *Run as administrator* (it needs to write under `Program Files (x86)\SimHub\`).
4. Walk through the four-step wizard:
   - **Welcome** — overview.
   - **Prerequisites** — auto-detects SimHub + F1 MultiViewer install paths, probes the MultiViewer API to confirm your F1 TV subscription is active **and** that Live Timing is actively streaming (a successful `SessionInfo` response — not just `Heartbeat`).
   - **Driver & source** — pick any driver from the dropdown (loaded live from MultiViewer's current grid, with a bundled fallback list). Choose data source (MultiViewer recommended — works for both live and replays).
   - **Install** — copies the plugin DLLs, dashboard files, writes `F1SimHubLive.Settings.json`, and restarts SimHub.
5. After install, in SimHub: enable the plugin under *Settings → Plugins*, then open *Dash Studio → F1RaceSim_GSIFPEV2* and select it on your wheel.

The installer is a single self-contained .exe (~90 MB) — no .NET runtime install required on the target machine. Source for the installer lives under [`installer/`](installer/).

### Update check (built into the installer)

On launch, the installer asks the GitHub Releases API whether a newer version exists. If yes, a yellow banner appears at the top of the Welcome page with a **Download** button (opens the latest release in your browser) and a **Continue** button (proceed with what you have). The check runs once per launch, has a 3-second timeout, and **never blocks install** — if you're offline, GitHub is rate-limiting, or anything else goes wrong, the banner simply stays hidden and the installer behaves exactly as before. This means an installer .exe sitting in your Downloads folder for months won't silently put you out of date — it will tell you when you run it.

The installer also reads `FileVersionInfo` of any existing `F1SimHubLive.dll` already deployed under your SimHub directory and logs both the existing and freshly-installed versions to the deploy log pane, so upgrades are explicit (e.g. *"Existing F1SimHubLive.dll detected — version 1.1.0. … Installed F1SimHubLive.dll version 1.1.1."*) rather than silent overwrites.

---

## Fresh-machine setup (first-time GSI wheel)

If the target PC has **never had a [GSI Formula Pro Elite V2](https://gomezsimindustries.com/products/formula-pro-elite-v2) wheel connected**, follow this exact order. Doing it out of order is the single most common cause of "wheel shows up but LCD/LEDs don't work" headaches.

### Why order matters

When Windows sees a new USB HID device, it auto-binds a **generic HID driver**. That driver is enough to expose buttons and axes to games, but it does **not** expose the wheel's LCD, RGB LEDs, or programmable features. The vendor companion (GSI SimOS) installs the device profile that unlocks those — but only if it's installed **before** the wheel is first enumerated. If you plug in first and install second, you may end up with a partially-bound device that needs to be unplugged + replugged before the full feature set comes online.

### Recommended install order

1. **Install SimHub** — <https://www.simhubdash.com/>. Default install path (`C:\Program Files (x86)\SimHub\`). Run it once so the first-launch wizard completes.
2. **Install F1 MultiViewer** — <https://multiviewer.app/>. Sign in with your F1 TV Pro account. Start a session (live or replay) **and open Live Timing** — for replays, click the **"Replay Live Timing"** button on the session card. Confirm `http://localhost:10101/api/v1/live-timing/SessionInfo` returns populated JSON in your browser. *(MultiViewer is only needed if you'll use the `MultiViewer` data source. The `F1Live` source talks to F1's broadcast SignalR feed directly and does not need MultiViewer. Note: just watching the F1 video stream inside MultiViewer is not enough — telemetry only flows once Live Timing is running.)*
3. **Install GSI SimOS** — get the latest installer from the wheel's product page at <https://gomezsimindustries.com/products/formula-pro-elite-v2>. **Do this with the wheel UNPLUGGED.** Reboot if the installer asks you to.
4. **Plug the wheel into USB** (wheel powered off → plug → power on, or follow the order in your wheel's quick-start card). Windows will run final HID enumeration; SimOS should pop up or sit in the tray and recognize the wheel.
5. **Open SimOS** and verify the wheel is detected. If it prompts for a firmware update, run it now — *do not unplug the wheel mid-update*. Wait for the "complete" confirmation before doing anything else.
6. **Open SimHub** → *Settings → Devices* → confirm the wheel appears (typically as a GSI device on a HID path). Add it as a controllable device if SimHub doesn't auto-add it.
7. **Run `F1SimHubLive-Installer.exe`** (this repo's installer) as administrator. The wizard auto-detects SimHub + MultiViewer, lets you pick a driver, deploys the plugin DLLs + dashboard, and restarts SimHub.
8. **In SimHub** → *Settings → Plugins* → enable **F1SimHubLive**. Then *Dash Studio → F1RaceSim_GSIFPEV2* → assign it to the GSI device.

### If you already plugged the wheel in first

Not catastrophic. Do this:

1. Close SimHub.
2. **Unplug the wheel** from USB and power it off.
3. Install GSI SimOS.
4. Reboot.
5. Plug the wheel back in, power it on, let SimOS finish enumeration.
6. Continue from step 5 above.

### Quick verification before you bother with the dashboard

In **Device Manager**, the wheel should appear under *Human Interface Devices* with no yellow warning triangle. In SimHub *Devices*, button presses should register a green ring around the input list. If both of those are clean, the plugin + dashboard install on top will work.

---

## What it does

**Live telemetry (60 Hz interpolated):**
- RPM, RpmPercent (0–100 normalized over 13,000)
- Gear (0–8)
- Speed (km/h)
- Throttle / Brake (0–100)
- DRS (raw code + `DrsActive` / `DrsEligible` bool)

**Per-driver timing (1 Hz race-control refresh):**
- Position (1st–20th)
- Lap, CurrentLap / TotalLaps, `LapDisplay` (e.g. `47/53`)
- BestLapTime, LastLapTime
- GapToLeader, IntervalToAhead
- InPit, PitStopCount
- TyreCompound + short letter (`S`/`M`/`H`/`I`/`W`), TyreAge
- Sector 1/2/3 times + personal-best + overall-best flags
- Ahead driver's sectors + **AheadCarNumber**
- Leader's sectors + **LeaderCarNumber**
- TopSpeed in km/h + TopSpeedRank
- OvertakeSystemEnabled / OvertakeAvailable

**Session state:**
- SessionTimeRemaining (`HH:MM:SS`)
- TrackStatus (text) and **TrackStatusCode** (1=AllClear, 2=Yellow, 3=GreenAll, 4=SC, 5=Red, 6=VSC, 7=VSC_Ending)
- FlagText — last race-control flag broadcast (GREEN, YELLOW, DOUBLE YELLOW, SC, VSC, RED, CHEQUERED)
- TotalDrivers

**Weather:**
- AirTemp °C, TrackTemp °C, Humidity %, Rainfall bool, WindSpeedKph

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ Data source (one of)                                             │
│  • F1SignalRClient  → wss://livetiming.formula1.com (broadcast)  │
│  • MultiViewerHttpClient → http://localhost:10101 (replay)       │
└─────────────────────────────────────────────────────────────────┘
                        │ raw SignalR/JSON
                        ▼
┌─────────────────────────────────────────────────────────────────┐
│ Decoders (per F1 topic)                                          │
│  CarData.z         → DriverSnapshot   (RPM/Gear/Speed/Throt/etc) │
│  TimingData        → TimingSnapshot   (Pos/Gap/Sectors/etc)      │
│  TimingStats       → TopSpeed                                    │
│  TimingAppData     → Tyre compound/age, pit stops                │
│  WeatherData       → WeatherSnapshot                             │
│  TrackStatus       → SessionSnapshot.TrackStatusCode/Message     │
│  LapCount          → CurrentLap / TotalLaps                      │
│  RaceControl       → FlagText                                    │
│  SessionData       → SessionTimeRemaining                        │
│  DriverList        → driver→racing-number map                    │
│  ExtrapolatedClock → session clock fallback                      │
└─────────────────────────────────────────────────────────────────┘
                        │
            ┌───────────┴──────────────┐
            ▼                          ▼
    TelemetryBuffer          OnTimingSnapshot/OnSessionSnapshot/
    (prev + curr car         OnWeatherSnapshot/OnStatus events
     snapshot, ring)
            │
            ▼
    Interpolator (60 Hz)
            │
            ▼
    F1SimHubLivePlugin.DataUpdate / per-event setters
            │
            ▼
    SimHub PluginManager.SetPropertyValue(...)
            │
            ▼
    F1RaceSim_GSIFPEV2.djson (Dash Studio) → GSI wheel HID screen + LEDs
```

**Why 60 Hz interpolation?** The broadcast multiplexes ~20 cars onto a single feed; per-car samples arrive at roughly 3–10 Hz with jitter. The plugin holds a ~200 ms render buffer and linearly interpolates between the last two snapshots so shift lights, throttle bars and RPM gauges look smooth instead of stepping.

---

## Two data sources: F1 Live vs MultiViewer

Set `Source` in `F1SimHubLive.Settings.json`:

| Value | What it connects to | When to use |
|---|---|---|
| `F1Live` (default) | `livetiming.formula1.com` SignalR 2.x hub `Streaming` | Live sessions only (FP1/2/3, Q, Sprint, Race). Data flows only while F1 is actively broadcasting. |
| `MultiViewer` | Local F1 MultiViewer app at `http://localhost:10101` | Replays from F1 TV recordings, paused sessions, or testing outside live windows. Requires MultiViewer running with a session loaded **and Live Timing actively running** — for replays, click "Replay Live Timing" on the session. Watching only the video feed produces no telemetry. |

The F1Live source has zero local dependencies. MultiViewer mode lets you scrub through past races for testing — used heavily during development to validate the dashboard against known SC/VSC/yellow events.

---

## SimHub property reference

All properties are exposed under the **`F1SimHubLivePlugin`** namespace (class name, not `[PluginName]` attribute). In Dash Studio bindings use `$prop('F1SimHubLivePlugin.X')` or `[F1SimHubLivePlugin.X]`.

### Car telemetry (interpolated 60 Hz)
| Property | Type | Range / values |
|---|---|---|
| `Rpm` | double | 0–~15000 |
| `RpmPercent` | double | 0–100 (normalized over 13000) |
| `Gear` | int | 0=N/R, 1–8 |
| `Speed` | double | km/h |
| `Throttle` | double | 0–100 |
| `Brake` | double | 0–100 |
| `Drs` | int | raw DRS code |
| `DrsActive` | bool | true if 10/12/14 |
| `DrsEligible` | bool | true if eligibility flag set |

### Driver timing
| Property | Notes |
|---|---|
| `Position` | string, current finishing position |
| `Lap` | this driver's lap counter |
| `BestLapTime` / `LastLapTime` | formatted `M:SS.ddd` |
| `GapToLeader` | `+12.345` or `+1 LAP` |
| `IntervalToAhead` | gap to car directly ahead |
| `InPit` | bool |
| `TyreCompound` / `TyreCompoundShort` | `SOFT` / `S`, etc. |
| `TyreAge` | int laps |
| `PitStopCount` | int |
| `TopSpeed` | string km/h |
| `TopSpeedRank` | int (1 = fastest in field) |
| `OvertakeSystemEnabled` | bool |
| `OvertakeAvailable` | bool |

### Sectors (this driver)
| Property | Type |
|---|---|
| `Sector1Time` / `Sector2Time` / `Sector3Time` | string |
| `SectorNIsPersonalBest` | bool (green) |
| `SectorNIsOverallBest` | bool (purple) |

### Sectors (driver ahead + race leader)
| Property | Notes |
|---|---|
| `AheadCarNumber` | F1 racing number of car directly in front |
| `LeaderCarNumber` | F1 racing number of current leader |
| `AheadSectorNTime` / `AheadSectorNIs(Personal/Overall)Best` | mirrors above |
| `LeaderSectorNTime` / `LeaderSectorNIs(Personal/Overall)Best` | mirrors above |

### Session
| Property | Type |
|---|---|
| `CurrentLap` / `TotalLaps` | int |
| `LapDisplay` | string `47/53` |
| `SessionTimeRemaining` | string `HH:MM:SS` |
| `TrackStatus` | string |
| `TrackStatusCode` | int (see below) |
| `FlagText` | string (RC broadcast) |
| `TotalDrivers` | int |

**TrackStatusCode values:**
| Code | Meaning |
|---|---|
| 1 | AllClear |
| 2 | Yellow |
| 3 | Green (transitional after yellow) |
| 4 | Safety Car (SC) |
| 5 | Red Flag |
| 6 | VSC Deployed |
| 7 | VSC Ending |

### Weather
`AirTemp` (°C), `TrackTemp` (°C), `Humidity` (%), `Rainfall` (bool), `WindSpeedKph`.

### Meta
| Property | Notes |
|---|---|
| `Source` | `F1Live` or `MultiViewer` |
| `CurrentDriverNumber` | driver being tracked |
| `Status` | connection state (`Initializing` → `Connecting` → `Connected` → …) |

### Driver identity (auto-resolved from DriverList)
Populated once per session as soon as the upstream `DriverList` is fetched. Empty strings until then.

| Property | Example | Notes |
|---|---|---|
| `DriverTla` | `VER` | Three-letter code |
| `DriverFirstName` | `Max` | As provided by F1 feed |
| `DriverLastName` | `Verstappen` | Use `.toUpperCase()` in dashboard for broadcast style |
| `DriverFullName` | `Max VERSTAPPEN` | Feed-provided full name |
| `DriverBroadcastName` | `M VERSTAPPEN` | F1 broadcast convention; synthesized when feed omits it |
| `TeamName` | `Red Bull Racing` | |
| `TeamColour` | `3671C6` | Team accent hex (no leading `#`) |

---

## F1RaceSim_GSIFPEV2 dashboard

`F1RaceSim_GSIFPEV2` is a custom Dash Studio template that ships in `dashboards/F1RaceSim_GSIFPEV2.djson`. It mimics the F1 TV broadcast graphic layout, scaled for the GSI wheel's 800×480 screen.

### Layout (top-to-bottom, left-to-right)

```
┌──────────────────────────────────────────────────────────────┐
│ [▲ Flag indicator] [Lap M/N] [Position]   [Driver #] [Pos] │  top strip
│  YELLOW/SC/VSC                                                │
├──────────────────────────────────────────────────────────────┤
│        Throttle / brake bars                                  │
│        ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━      │
│        @vicslive                                              │
│        github · instagram                                     │
├──────────────────────────────────────────────────────────────┤
│ [INT car #] AheadSect1/2/3            LAST   1:23.456        │
│ [LDR car #] LeaderSect1/2/3           GAP    +12.345         │
├──────────────────────────────────────────────────────────────┤
│ [TYRE] [STOPS] [TOP SPEED] [OVERTAKE] [F1Flag]                │  bottom strip
└──────────────────────────────────────────────────────────────┘
```

### Widget binding map

| Widget | Bound to | Notes |
|---|---|---|
| Shift lights (LEDs) | `RpmPercent` | 12000 RPM ≈ 92% → all green; 13000 RPM = 100% red. |
| Speed | `Speed` | |
| Gear | `Gear` | |
| Throttle bar | `Throttle` | |
| Brake bar | `Brake` | |
| DRS indicator | `DrsActive` / `DrsEligible` | |
| Position | `Position` | |
| Lap display (M/N) | `LapDisplay` | |
| `AheadNumber` | `AheadCarNumber` | shown to the LEFT of the INT sectors row |
| `BehindNumber` | `LeaderCarNumber` (blank if `Position==1`) | shown to the LEFT of the LDR sectors row |
| LAST / GAP cluster | `LastLapTime` / `GapToLeader` | Manually-calibrated coords inside the V4 background frame |
| Sector 1/2/3 | `SectorNTime` + `SectorNIs(Personal/Overall)Best` for color | own driver |
| INT sectors row | `AheadSectorNTime` + ahead best flags | car directly in front |
| LDR sectors row | `LeaderSectorNTime` + leader best flags | race leader |
| Tyre | `TyreCompoundShort` + `TyreAge` | |
| Stops | `PitStopCount` | |
| Top Speed | `TopSpeed` + `TopSpeedRank` | |
| Overtake | `OvertakeAvailable` | |
| Top-left triangle (`INCLogo` + `IncCount`) | `TrackStatusCode` | Repurposed from iRacing incidents counter. Shows when code ∈ {2,4,5,6,7}. Text: YELLOW / SC / RED / VSC. Color: red for RED flag, amber otherwise. |
| Bottom-right `F1Flag` | `FlagText` (priority) → fallback `TrackStatusCode` | Synced with top triangle. Green for CLEAR/GREEN, amber for YELLOW/SC/VSC/DOUBLE YELLOW, red for RED, white for CHEQUERED. |
| `@vicslive` signature | static | Personal handle widget between throttle graph and LAST/GAP cluster. |
| Weather strip (when shown) | `AirTemp` / `TrackTemp` / `Rainfall` | |
| Session clock | `SessionTimeRemaining` | |

### Caution status — two complementary widgets

The dashboard uses **two** flag indicators that stay in sync:

- **Top-left triangle** (`INCLogo` red hazard + `IncCount` text): driven by `TrackStatusCode` (persistent track state). Hidden when CLEAR.
- **Bottom-right `F1Flag`**: driven by `FlagText` (race-control broadcast); falls back to `TrackStatusCode` when no active RC message so the two stay aligned during VSC/SC/YELLOW.

**Color convention** (per F1 broadcast standard):
- 🟢 Green text = CLEAR / GREEN
- 🟡 Amber text + 🔺 red triangle = YELLOW / SC / VSC (race continues, caution active)
- 🔴 Red text + 🔺 red triangle = RED flag (race halted)
- ⚪ White text = CHEQUERED (race finished)

---

## Build the plugin

**Requirements**
- Windows
- SimHub 9.x installed at `C:\Program Files (x86)\SimHub`
- .NET SDK 8.0 (build-time only) — `winget install Microsoft.DotNet.SDK.8`
- .NET Framework 4.8 runtime (already present if SimHub runs)

**Build**
```powershell
cd $env:USERPROFILE\F1SimHubLive
dotnet restore
dotnet build -c Release
```

**Output location** (important — not `bin\Release\net48\`):
```
%USERPROFILE%\F1SimHubLive\bin\Release\F1SimHubLive.dll
%USERPROFILE%\F1SimHubLive\bin\Release\Microsoft.AspNet.SignalR.Client.dll
%USERPROFILE%\F1SimHubLive\bin\Release\Newtonsoft.Json.dll
```

**Auto-deploy.** After a successful Release build, an `AfterBuild` target invokes `scripts\deploy.ps1` to copy `F1SimHubLive.dll` into `C:\Program Files (x86)\SimHub\` and mirror `dashboards\F1RaceSim_GSIFPEV2\` into `C:\Program Files (x86)\SimHub\DashTemplates\F1RaceSim_GSIFPEV2\`. The deploy skips gracefully if SimHub is running (the DLL would be locked) or if SimHub is not installed — it never fails the build. **You still have to restart SimHub** to pick up the changes; the script prints a loud reminder when it finishes.

Opt out:

```powershell
dotnet build -c Release -p:DeploySimHub=false
```

One-shot dev iteration (deploy + relaunch SimHub) — assumes SimHub is already closed:

```powershell
dotnet build -c Release -p:StartSimHub=true
```

Or run the deploy step on its own after a build:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\deploy.ps1                # deploy only
powershell -ExecutionPolicy Bypass -File scripts\deploy.ps1 -StartSimHub   # deploy + launch
# scripts\deploy.ps1 -DllOnly         # plugin only, skip dashboards
# scripts\deploy.ps1 -DashboardsOnly  # dashboards only, skip plugin
```

---

## Install (manual)

> **Easier path:** use the [Quick install (installer)](#quick-install-installer) above. The manual steps below are for developers building from source.

### Plugin

1. Close SimHub.
2. Copy from `bin\Release\` to `C:\Program Files (x86)\SimHub\`:
   - `F1SimHubLive.dll` (required)
   - `Microsoft.AspNet.SignalR.Client.dll` (required, ships with the plugin)
   - `Newtonsoft.Json.dll` (only if SimHub doesn't already ship a compatible version)
3. Copy `F1SimHubLive.Settings.example.json` to that same folder as `F1SimHubLive.Settings.json` (next to the DLL).
4. Start SimHub. On first run it asks to enable the new plugin — say yes.

### F1RaceSim_GSIFPEV2 dashboard

1. Copy `dashboards/F1RaceSim_GSIFPEV2.djson` to:
   ```
   C:\Program Files (x86)\SimHub\DashTemplates\F1RaceSim_GSIFPEV2\F1RaceSim_GSIFPEV2.djson
   ```
2. Copy any background images referenced by the dashboard (V4 background, F1 logos, tyre icons) into the same folder.
3. In SimHub → Dash Studio, open the F1RaceSim_GSIFPEV2 template.
4. In your GSI wheel device profile, target the F1RaceSim_GSIFPEV2 dashboard.

---

## Configure

`F1SimHubLive.Settings.json` (lives next to the DLL):

```json
{
  "DriverNumber": "44",
  "OutputHz": 60,
  "RenderDelayMs": 200,
  "Source": "F1Live",
  "MultiViewerBaseUrl": "http://localhost:10101",
  "MultiViewerPollMs": 250,
  "MultiViewerTimingPollMs": 1000
}
```

| Key | Default | Meaning |
|---|---|---|
| `DriverNumber` | `"44"` | F1 racing number string. `44`=Hamilton, `1`=Verstappen, `16`=Leclerc, `81`=Piastri, `4`=Norris, `63`=Russell, `55`=Sainz, `14`=Alonso, `11`=Pérez, `18`=Stroll. |
| `OutputHz` | `60` | Interpolation tick rate for car telemetry. 60 is plenty for LEDs; higher just uses more CPU. |
| `RenderDelayMs` | `200` | Render lag. Holds 200ms of buffer so the interpolator always has `prev` + `curr` snapshots to interpolate between. Lower = less added latency but more "hold" between samples. |
| `Source` | `"F1Live"` | `F1Live` (broadcast SignalR) or `MultiViewer` (local replay). |
| `MultiViewerBaseUrl` | `http://localhost:10101` | F1 MultiViewer HTTP API root. Only used when `Source=MultiViewer`. |
| `MultiViewerPollMs` | `250` | Car-data polling interval against MultiViewer (4 Hz default). |
| `MultiViewerTimingPollMs` | `1000` | Timing/session/weather polling interval against MultiViewer (1 Hz default). |

Restart SimHub after editing.

---

## Run a session

**Live mode (default):**
1. F1 session is broadcasting on F1 TV.
2. `Source=F1Live` in settings.
3. Start SimHub → check Plugins panel → F1SimHubLive status should reach `Connected`.
4. Properties populate within ~10s of session start.

**Replay mode:**
1. Open F1 MultiViewer and sign in to F1 TV.
2. Load the session you want to replay.
3. **Click "Replay Live Timing"** on that session — this is the step that makes MultiViewer start emitting telemetry to `http://localhost:10101`. Watching only the F1 video feed is **not enough**; the Live Timing view must be running.
4. Set `Source=MultiViewer` in settings.
5. Restart SimHub.
6. Scrub/play in MultiViewer; properties follow.

**Verify the plugin is feeding properties** (handy for debugging):
```powershell
foreach ($p in 'Status','Rpm','Gear','Speed','Position','LapDisplay','TrackStatusCode','FlagText') {
  $v = curl.exe -s "http://127.0.0.1:8888/api/getproperty/F1SimHubLivePlugin.$p"
  Write-Host ("{0,-22} = {1}" -f $p,$v)
}
```

(Requires SimHub's HTTP API enabled in Settings.)

---

## Troubleshooting

**Status stays `Initializing` or `Connecting`:**
- F1Live: confirm an F1 session is actually broadcasting on F1 TV. Outside session windows the feed is empty.
- MultiViewer: confirm MultiViewer is running with a session loaded **and Live Timing actively running**. The fastest check: open `http://localhost:10101/api/v1/live-timing/SessionInfo` in a browser — if it returns 404 or an empty body, Live Timing is not on. For replays, click **"Replay Live Timing"** on the session card inside MultiViewer; the video player alone does not emit telemetry. See [`docs/multiviewer-api.md`](docs/multiviewer-api.md) for the full two-stage probe rationale and a step-by-step manual verification recipe.

**Properties show but RPM/Gear stay at 0:**
- The CarData topic is per-driver. Confirm `DriverNumber` matches a driver currently in the field. Spelling/case doesn't matter — F1 uses raw integers as strings.

**Shift lights look choppy:**
- Lower `RenderDelayMs` toward 100. Below 100 you'll start to see hold (one sample staying put) before the next arrives.

**Dashboard widget shows nothing / `--`:**
- If the widget is inside a Layer group (e.g. `IncidentData`), the group's `Visible` flag overrides every child. Set the **group** `Visible:true` and let child bindings drive individual visibility.
- Widget-level `"Visible":false` (a static property) also overrides `Bindings.Visible`. Set the static property to `true` if you want a binding to control it.

**`Newtonsoft.Json` version conflict on SimHub startup:**
- Remove `Newtonsoft.Json.dll` from `C:\Program Files (x86)\SimHub\` and use the one SimHub ships.

---

## File layout

```
%USERPROFILE%\F1SimHubLive\
├── F1SimHubLivePlugin.cs            # Entry point; property registration + event wiring
├── Settings.cs                     # JSON settings model
├── F1SimHubLive.csproj              # .NET 4.8 class library
├── F1SimHubLive.Settings.example.json
├── README.md                       # this file
├── F1Signalr/
│   ├── F1SignalRClient.cs          # Live SignalR client (livetiming.formula1.com)
│   ├── CarDataDecoder.cs           # base64 → DEFLATE → JSON → DriverSnapshot
│   └── TopicNames.cs
├── MultiViewer/
│   ├── MultiViewerHttpClient.cs    # Local MultiViewer HTTP polling
│   ├── TimingDataDecoder.cs        # Position/Gap/Sectors + Ahead/Leader car numbers
│   ├── TimingStatsDecoder.cs       # TopSpeed + rank
│   ├── TimingAppDataDecoder.cs     # Tyre + stops
│   ├── SessionDataDecoder.cs       # Session clock
│   ├── TrackStatusDecoder.cs       # Track status enum
│   ├── LapCountDecoder.cs          # CurrentLap/TotalLaps
│   ├── WeatherDataDecoder.cs       # Weather snapshot
│   ├── RaceControlDecoder.cs       # FlagText
│   ├── DriverListDecoder.cs        # driver # → metadata
│   └── ExtrapolatedClockDecoder.cs # Session clock fallback
└── Telemetry/
    ├── ITelemetrySource.cs         # Common interface for both sources
    ├── DriverSnapshot.cs           # RPM/Gear/Speed/etc — per car
    ├── TimingSnapshot.cs           # Per-driver timing
    ├── SessionSnapshot.cs          # Track status + session clock
    ├── WeatherSnapshot.cs
    ├── TelemetryBuffer.cs          # Ring of prev + curr snapshots
    └── Interpolator.cs             # 60 Hz linear interpolation

installer/                          # WPF installer wizard (.NET 8)
├── F1SimHubLive.Installer.csproj    # Single-file publish config
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs  # 4-step wizard UI
├── Models/                         # F1Driver, PrereqResult
├── Services/                       # PrereqChecker, DriverListService, Deployer
└── Assets/                         # Embedded plugin DLL, dashboard, drivers-fallback.json

dashboards/                         # Source-of-truth Dash Studio templates
└── F1RaceSim_GSIFPEV2/                      # Deployed by the installer to SimHub\DashTemplates\

scripts/
└── refresh-drivers.ps1             # Pull current grid from MultiViewer into drivers-fallback.json

.github/workflows/
└── release.yml                     # Tag-triggered build + (optional) Trusted Signing

CHANGELOG.md                        # Version history
DASHBOARD.md                        # Widget-level reference for F1RaceSim_GSIFPEV2.djson
SIGNING.md                          # Code-signing options for the installer
LICENSE                             # MIT
```

Dashboard template lives in SimHub's install dir (not this repo):
```
C:\Program Files (x86)\SimHub\DashTemplates\F1RaceSim_GSIFPEV2\
├── F1RaceSim_GSIFPEV2.djson                 # the dashboard definition
└── (background images, tyre icons, F1 logos)
```

---

## Known limitations

- **No ERS state** — lives in `TimingAppData` but not yet decoded (`v2` candidate).
- **No track position** — `Position.z` exists in the feed but not parsed (could drive a circuit-map widget).
- **No settings GUI** — edit JSON and restart SimHub.
- **Live mode only works during active F1 sessions** (FP1/2/3, Q, Sprint, Race). Outside that window the SignalR connection succeeds but no `feed` messages arrive. Use MultiViewer source for replay.
- **F1 broadcast telemetry is 3–10 Hz per car.** No client can do better than that — the interpolator smooths it but doesn't add information.
- **SC and RED flag visual states untested in production.** The bindings use the same code paths as the confirmed YELLOW/VSC states; should work but unverified on live wheel.

---

## License

Released under the [MIT License](LICENSE) — Copyright © 2026 Victor de Souza ([@vicslive](https://github.com/vicslive)). Fork freely, contribute back if you'd like.

F1 live timing data is proprietary to Formula 1. This plugin is a fan tool and is not affiliated with Formula 1, F1 MultiViewer, SimHub, GSI, or any team.

---

## Companion docs

| Doc | What's in it |
|---|---|
| [CHANGELOG.md](CHANGELOG.md) | Version history. v1.0.1 ships the verified 2026 grid; v1.0.0 was the first public installer. |
| [DASHBOARD.md](DASHBOARD.md) | Implementer's reference for `F1RaceSim_GSIFPEV2.djson` — every widget, binding, and the gotchas discovered while building it. |
| [docs/multiviewer-api.md](docs/multiviewer-api.md) | Why "MultiViewer is open" is not enough — the API-up vs Live-Timing-streaming distinction, the two-stage `Heartbeat`+`SessionInfo` probe the installer uses, a 5-step manual verification recipe, and a reference table of every endpoint the plugin polls. |
| [SIGNING.md](SIGNING.md) | Code-signing options for the installer ranked by cost/UX. Includes the Microsoft Trusted Signing employee-credit path and the SFI workaround. |
| [scripts/refresh-drivers.ps1](scripts/refresh-drivers.ps1) | Pulls the current season's `DriverList` from a running MultiViewer and rewrites `installer/Assets/drivers-fallback.json`. Run at the start of each season. |
| [.github/workflows/release.yml](.github/workflows/release.yml) | GitHub Actions release pipeline — builds the installer on every `v*.*.*` tag, signs it via `azure/trusted-signing-action` if signing secrets are configured. |

---

## Contributing

PRs, issue reports, and forks are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for the development setup, what's in/out of scope, and the PR checklist.

---

## Credits

- **SimHub** by Wotever — the platform that makes wheel telemetry possible.
- **F1 MultiViewer** — the inspiration for replay-mode support and the source-of-truth for the broadcast topics.
- **FastF1** — invaluable reference for the CarData channel numbering and SignalR topic semantics.
- **[GSI (Gomez Sim Industries)](https://gomezsimindustries.com/products/formula-pro-elite-v2)** — the Formula Pro Elite V2 wheel this was built around.

Built by **Victor de Souza** (`@vicslive`) — personal hack to make F1 broadcasts more immersive on the rig.
