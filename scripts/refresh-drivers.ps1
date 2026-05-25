# refresh-drivers.ps1
# Pulls the current driver list from a running F1 MultiViewer install
# and writes it to installer/Assets/drivers-fallback.json.
#
# Usage:
#   1. Start F1 MultiViewer and sign in to F1 TV.
#   2. Open a current-season session (any practice/quali/race or replay).
#   3. From the repo root, run:
#         pwsh scripts/refresh-drivers.ps1
#   4. Commit installer/Assets/drivers-fallback.json.
#
# At install time, the wizard prefers live MV data and only falls back to
# this file when MV isn't running — so refreshing is only needed when you
# want offline-install machines to see the correct season grid.

[CmdletBinding()]
param(
    [string]$MvBaseUrl = "http://localhost:10101",
    [string]$OutputPath = (Join-Path $PSScriptRoot ".." "installer/Assets/drivers-fallback.json"),
    [int]$Season = 2026
)

$ErrorActionPreference = "Stop"

Write-Host "Probing $MvBaseUrl ..."
try {
    $resp = Invoke-WebRequest -Uri "$MvBaseUrl/api/v1/live-timing/DriverList" -TimeoutSec 5 -UseBasicParsing
} catch {
    Write-Error "Could not reach MultiViewer at $MvBaseUrl. Make sure MV is running, signed in, and a session is loaded."
    exit 1
}

if ($resp.StatusCode -ne 200) {
    Write-Error "MV returned HTTP $($resp.StatusCode) — is a session loaded?"
    exit 1
}

$data = $resp.Content | ConvertFrom-Json
$drivers = @()
foreach ($prop in $data.PSObject.Properties) {
    if ($prop.Name -notmatch '^\d+$') { continue }
    $d = $prop.Value
    $drivers += [PSCustomObject]@{
        number    = [int]$prop.Name
        code      = $d.Tla
        firstName = $d.FirstName
        lastName  = $d.LastName
        team      = $d.TeamName
    }
}
$drivers = $drivers | Sort-Object number

if ($drivers.Count -eq 0) {
    Write-Error "DriverList returned no drivers — load a session in MV and try again."
    exit 1
}

$out = [ordered]@{
    "_comment" = "Auto-generated from F1 MultiViewer DriverList on $(Get-Date -Format 'yyyy-MM-dd'). Refresh by re-running scripts/refresh-drivers.ps1, or simply let the installer pull live from MV at install time."
    season     = $Season
    generated  = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')
    drivers    = $drivers
}

$resolvedPath = (Resolve-Path -LiteralPath (Split-Path $OutputPath -Parent)).Path
$finalPath = Join-Path $resolvedPath (Split-Path $OutputPath -Leaf)

$json = $out | ConvertTo-Json -Depth 5
$json | Out-File -LiteralPath $finalPath -Encoding utf8 -NoNewline

Write-Host ""
Write-Host "Wrote $($drivers.Count) drivers to $finalPath"
Write-Host ""
$drivers | Format-Table -AutoSize
