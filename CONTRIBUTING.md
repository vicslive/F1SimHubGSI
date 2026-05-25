# Contributing to F1SimHubLive

Thanks for considering a contribution. This is a personal hack built by [@vicslive](https://github.com/vicslive) for his own race-watching rig, but it's MIT-licensed and you're welcome to fork, file issues, or send pull requests.

## What's in scope

- **New telemetry properties** — anything decoded from the F1 SignalR feed or MultiViewer's HTTP topics that doesn't already surface as a `F1SimHubLivePlugin.*` property.
- **Dashboard improvements** to the bundled `F1RaceSim_GSIFPEV2` template — bug fixes, polish, wheel-size variants.
- **Installer improvements** — better prereq detection, additional source toggles, smarter driver dropdown (e.g. team grouping, headshots).
- **Build/release ergonomics** — CI improvements, signing automation, packaging.
- **Documentation** — typo fixes, clarifications, screenshots.

## What's out of scope

- **Hard-coded driver favorites or paywalled features.** The installer's driver dropdown stays open to anyone in the grid.
- **Anything that violates F1 / F1 MultiViewer / SimHub terms of service.** This is a fan tool that consumes data the user already has legitimate access to (F1 TV Pro subscription, MultiViewer license). It doesn't scrape, redistribute, or rebroadcast F1 data.
- **Wheels that aren't the GSI Formula Pro Elite V2.** The plugin's property tree is generic — any SimHub dashboard can consume it — but the bundled `F1RaceSim_GSIFPEV2` template is specifically calibrated for the GSI Formula Pro Elite V2 800×480 LCD. If you want a different size, fork the dashboard.

## Development setup

See the [Build the plugin](README.md#build-the-plugin) and [Install (manual)](README.md#install-manual) sections in the README. Short version:

1. Windows + SimHub installed at `C:\Program Files (x86)\SimHub\`.
2. .NET SDK 8 (`winget install Microsoft.DotNet.SDK.8`).
3. `dotnet restore && dotnet build -c Release`.
4. Drop the resulting DLLs into `SimHub\`, restart SimHub.

For the installer:

```powershell
cd installer
dotnet publish -c Release -o publish
```

For the dashboard, edit `dashboards/F1RaceSim_GSIFPEV2/F1RaceSim_GSIFPEV2.djson` in a text editor or open it in SimHub Dash Studio. [DASHBOARD.md](DASHBOARD.md) is the implementer's reference.

## Pull request checklist

- [ ] Code builds clean: `dotnet build -c Release` produces no warnings beyond the existing baseline.
- [ ] If you added a property, it's registered in `F1SimHubLivePlugin.cs` and documented in [README — SimHub property reference](README.md#simhub-property-reference).
- [ ] If you touched the dashboard, [DASHBOARD.md](DASHBOARD.md) is updated.
- [ ] Commit messages follow Conventional Commits where reasonable (`feat:`, `fix:`, `docs:`, `refactor:`, `chore:`).
- [ ] [CHANGELOG.md](CHANGELOG.md) has an entry under `[Unreleased]`.
- [ ] PR description explains the motivation and what testing you did. Mention the specific F1 session (FP/Quali/Race) or replay you tested against.

## Filing an issue

Useful issues include:

- **Logs.** SimHub writes to `%APPDATA%\SimHub\Logs\`. Attach the relevant chunk (filter to lines mentioning `F1SimHubLive`).
- **Source.** Whether you were running `Source=F1Live` or `Source=MultiViewer` when the bug hit.
- **Session context.** What was happening on track when the symptom showed up (lap, on-screen flag state, pit-window activity).
- **Repro.** If you can reproduce with a MultiViewer replay, name the session and timestamp.

## Releases

Maintainer-only: see [SIGNING.md](SIGNING.md) for the release + signing pipeline. TL;DR: tag `vX.Y.Z` on `main`, push the tag, the GitHub Actions workflow builds and publishes the installer. If Trusted Signing secrets are configured, the .exe is signed.

## Code of conduct

Be respectful. Don't be a jerk in issues or PRs. Personal attacks, harassment, or discrimination of any kind will get you blocked from the repo. We're here to make F1 broadcasts more immersive, not to argue.
