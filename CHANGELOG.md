# Changelog

All notable changes to F1SimHubLive and the companion `F1RaceSim_GSIFPEV2` dashboard.

Format follows [Keep a Changelog](https://keepachangelog.com/). Dates in `YYYY-MM-DD`.

## [Unreleased]

## [1.0.0] — 2026-05-25

### Added — first release

F1SimHubLive is a SimHub plugin + companion `F1RaceSim_GSIFPEV2` Dash Studio dashboard that pipes
live Formula 1 broadcast telemetry (via F1 Live SignalR or F1 MultiViewer's local HTTP API)
into a SimHub-connected wheel screen. The current dashboard is laid out for an 800×480
wheel screen and has been validated on the GSI Formula Pro Elite V2 and GSI Hyper P1.

**Plugin** (`F1SimHubLive.dll`, net48, runs inside SimHub):

- Dual telemetry source: `F1Live` (SignalR feed from `livetiming.formula1.com` — live broadcasts only) or `MultiViewer` (HTTP API at `localhost:10101` — works for live AND replays).
- 60 Hz interpolated render over a ~3–10 Hz upstream feed, configurable `OutputHz` / `RenderDelayMs`.
- Per-driver telemetry: RPM, gear, speed, throttle, brake, DRS state, lap time, sector splits, gap to leader, interval to ahead, tyre compound/age, pit-stop count, top-speed rank, overtake availability.
- **TopSpeed running-max** — the `TopSpeed` property is computed as the max of every live `Speed` sample plus the upstream `BestSpeeds.ST` (speed-trap) snapshot, so the dashboard never visually regresses when a driver hits a higher peak away from the trap. Sanity-capped at 450 km/h and reset on session boundaries.
- Session state: current lap / total laps, session time remaining, track status (YELLOW / SC / VSC / RED), race control flag text.
- Driver identity: TLA, first name, last name, full name, broadcast name, team name, team colour — resolved from MultiViewer's `DriverList` topic.
- Weather snapshot: air temp, track temp, humidity, rainfall, wind speed.

**Installer** (`F1SimHubLive-Installer.exe`, net8 WPF, single-file self-contained ~86 MB):

- Five-step wizard: Welcome → Prerequisites → Driver & source → Install → Done.
- Prereq checks: SimHub install path (auto-detected from registry + standard locations), F1 MultiViewer install path, MultiViewer Live Timing actively streaming (`Heartbeat` AND `SessionInfo` probes — Heartbeat alone is not enough), and **wheel device detection** (enumerates `PluginsData\Common\Devices\` so you can see exactly which screen(s) F1SimHubLive will target).
- Driver dropdown loaded live from MultiViewer's running grid with a bundled 2026 fallback list.
- **Idle dashboard consent** — opt-in checkbox to set `F1RaceSim_GSIFPEV2` as the SimHub idle dashboard on every detected screen. Timestamped backup of each device's `settings.json` written before mutation; declined choice leaves SimHub untouched and the Done page shows a warning explaining how to flip it manually.
- Self-update check on launch — yellow banner appears on Welcome when GitHub Releases reports a newer tag than the installed installer. 3-second timeout, silent on failure, never blocks install.
- Plugin DLL version logging during deploy — shows existing vs incoming `F1SimHubLive.dll` versions so upgrades are explicit, not silent.
- Stops SimHub, deploys plugin DLLs and dashboard files, writes `F1SimHubLive.Settings.json`, applies the idle-dashboard change, restarts SimHub.

**Dashboard** (`F1RaceSim_GSIFPEV2.djson`, 800×480):

- Broadcast-style layout with shift lights driven by live RPM.
- Top-center title shows the selected driver's last name (e.g. `HAMILTON`, `VERSTAPPEN`) so the wheel always makes it obvious which car the telemetry belongs to. Falls back to `F1 LIVE` for the brief window before the DriverList resolves.
- **Driver-name title renders in the live F1 team colour** — when the plugin is `Connected` and the upstream `TeamColour` resolves, the title paints in the broadcast-accurate hex (Ferrari `#E80020`, Mercedes `#27F4D2`, Red Bull `#3671C6`, etc.) so the wheel matches the on-screen TV graphic. Falls back to green on connect, red-orange on connecting, amber on disconnect.
- Left-side broadcast pills for car ahead (INT) and leader (LDR) with car numbers.
- LAST / GAP cluster, sector splits with personal-best (green) and overall-best (purple) flags.
- **INPUTS panel** — live throttle (white) and brake (yellow) bar charts driven by `F1SimHubLivePlugin.Throttle` and `F1SimHubLivePlugin.Brake`, rolling 100-point history.
- Flag/Caution indicator driven by `TrackStatusCode` (YELLOW / SC / VSC / RED).
- `@vicslive` signature widget.

**Build and release infrastructure**:

- `F1SimHubLive.csproj` includes an `AfterTargets="Build"` step that auto-deploys the plugin DLL and dashboard files into the local SimHub install. Opt out with `-p:DeploySimHub=false` (CI). Pass `-p:StartSimHub=true` to chain a SimHub relaunch onto a successful build.
- `scripts/deploy.ps1` — idempotent PowerShell deployer used by the MSBuild target. Skips gracefully when SimHub is running (DLL locked) or not installed. Excludes `*.bak-*`, `*.pre*-*`, `*.backup-*`.
- `.github/workflows/release.yml` — builds and publishes the installer on every `v*.*.*` tag push or via `workflow_dispatch`. Optional Microsoft Trusted Signing via `azure/trusted-signing-action` with federated identity (no long-lived secrets).
- `SIGNING.md` — full code-signing playbook (5 options ranked by UX and cost, signtool examples, timestamping rules, SFI gotcha for Microsoft employees on personal subscriptions).

**Documentation**: `README.md` (user + developer guide), `DASHBOARD.md` (widget reference), `docs/multiviewer-api.md` (MultiViewer endpoint table + why `SessionInfo` is the right liveness probe), `CONTRIBUTING.md`.