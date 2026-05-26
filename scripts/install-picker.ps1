<#
.SYNOPSIS
    Copies the locally-built F1SimHubLive Driver Picker into SimHub and
    creates a Start Menu shortcut for one-click launching.

.DESCRIPTION
    Use this when you've built the picker locally (dotnet publish picker/...)
    and want it deployed without going through the full installer rebuild.
    The release installer does the same thing for end users.

.PARAMETER SimHubPath
    SimHub install directory. Defaults to C:\Program Files (x86)\SimHub.

.PARAMETER NoShortcut
    Skip creating the Start Menu shortcut.

.NOTES
    Must be run as administrator if SimHub lives under Program Files (x86).
#>
[CmdletBinding()]
param(
    [string]$SimHubPath = "${env:ProgramFiles(x86)}\SimHub",
    [switch]$NoShortcut
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$published = Join-Path $repoRoot 'picker\bin\Release\net8.0-windows\win-x64\publish\F1SimHubLive-Picker.exe'

# Always publish — dotnet's incremental build is fast when nothing changed,
# and skipping it here is how stale exes get deployed.
Write-Host "Publishing picker (incremental)..." -ForegroundColor Yellow
Push-Location $repoRoot
try {
    dotnet publish picker\F1SimHubLive.Picker.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
} finally {
    Pop-Location
}
if (-not (Test-Path $published)) {
    throw "Publish reported success but exe not found at $published"
}

if (-not (Test-Path $SimHubPath)) {
    throw "SimHub install dir not found: $SimHubPath"
}

$dest = Join-Path $SimHubPath 'F1SimHubLive-Picker.exe'
Write-Host "Copying picker to $dest ..." -ForegroundColor Cyan
Copy-Item $published $dest -Force
Write-Host "  done." -ForegroundColor Green

if (-not $NoShortcut) {
    $startMenu = [Environment]::GetFolderPath('CommonStartMenu')
    $folder = Join-Path $startMenu 'Programs\F1SimHubLive'
    if (-not (Test-Path $folder)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
    $lnk = Join-Path $folder 'F1SimHubLive Driver Picker.lnk'

    Write-Host "Creating Start Menu shortcut: $lnk ..." -ForegroundColor Cyan
    $wsh = New-Object -ComObject WScript.Shell
    $sc = $wsh.CreateShortcut($lnk)
    $sc.TargetPath = $dest
    $sc.WorkingDirectory = $SimHubPath
    $sc.IconLocation = "$dest,0"
    $sc.Description = "Switch the watched F1 driver live for F1SimHubLive"
    $sc.WindowStyle = 1
    $sc.Save()
    Write-Host "  done." -ForegroundColor Green
}

Write-Host ""
Write-Host "Picker installed. Launch via Start Menu > F1SimHubLive > Driver Picker," -ForegroundColor Green
Write-Host "or set AutoLaunchPicker=true in F1SimHubLive.Settings.json to spawn it with SimHub." -ForegroundColor Green
