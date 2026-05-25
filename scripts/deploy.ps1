#requires -Version 5.1
<#
.SYNOPSIS
    Deploy the built F1SimHubLive plugin + dashboards into the SimHub install dir.

.DESCRIPTION
    Copies bin\Release\F1SimHubLive.dll into $SimHubPath and mirrors each
    dashboards\<Name>\ folder into $SimHubPath\DashTemplates\<Name>\, excluding
    *.bak-*, *.pre*, and the SimHub-managed _Backups\ folder.

    Invoked automatically by F1SimHubLive.csproj after Release builds (skip with
    -p:DeploySimHub=false). Skips gracefully (exit 0) when SimHub is running or
    when $SimHubPath does not exist - never fails the build.

    Run manually with no args after closing SimHub; defaults to repo root and
    Program Files SimHub.

.PARAMETER DllPath
    Path to the freshly built plugin DLL. Defaults to <repo>\bin\Release\F1SimHubLive.dll.

.PARAMETER SimHubPath
    SimHub install directory. Defaults to ${env:ProgramFiles(x86)}\SimHub.

.PARAMETER ProjectRoot
    Repo root used to find dashboards\. Defaults to the parent of this script.

.PARAMETER DashboardsOnly
    Skip the DLL copy and only sync dashboards.

.PARAMETER DllOnly
    Skip the dashboard sync and only copy the DLL.

.PARAMETER StartSimHub
    After a successful deploy, launch SimHubWPF.exe so changes load immediately.
    Has no effect if the deploy step skipped (e.g. SimHub still running).
#>
[CmdletBinding()]
param(
    [string] $DllPath,
    [string] $SimHubPath,
    [string] $ProjectRoot,
    [switch] $DashboardsOnly,
    [switch] $DllOnly,
    [switch] $StartSimHub
)

$ErrorActionPreference = 'Stop'

if (-not $ProjectRoot) {
    $ProjectRoot = Split-Path -Parent $PSScriptRoot
}
if (-not $DllPath) {
    $DllPath = Join-Path $ProjectRoot 'bin\Release\F1SimHubLive.dll'
}
if (-not $SimHubPath) {
    $progFiles86 = ${env:ProgramFiles(x86)}
    if (-not $progFiles86) { $progFiles86 = 'C:\Program Files (x86)' }
    $SimHubPath = Join-Path $progFiles86 'SimHub'
}

function Write-Info($msg) { Write-Host "[deploy] $msg" }
function Write-Skip($msg) { Write-Host "[deploy] SKIP: $msg" -ForegroundColor Yellow }
function Write-Ok($msg)   { Write-Host "[deploy] OK:   $msg" -ForegroundColor Green }

if (-not (Test-Path -LiteralPath $SimHubPath)) {
    Write-Skip "SimHub not installed at '$SimHubPath' - nothing to do."
    exit 0
}

$running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
    $_.ProcessName -like 'SimHub*'
})
if ($running.Count -gt 0) {
    $names = ($running | ForEach-Object { "$($_.ProcessName)($($_.Id))" }) -join ', '
    Write-Skip "SimHub is running ($names) - close it and rebuild to deploy."
    exit 0
}

Write-Info "SimHubPath  = $SimHubPath"
Write-Info "ProjectRoot = $ProjectRoot"

if (-not $DashboardsOnly) {
    if (-not (Test-Path -LiteralPath $DllPath)) {
        Write-Skip "DLL not found at '$DllPath' - build first."
    } else {
        $target = Join-Path $SimHubPath 'F1SimHubLive.dll'
        Copy-Item -LiteralPath $DllPath -Destination $target -Force
        $info = Get-Item -LiteralPath $target
        $msg = "DLL -> {0} ({1} bytes, {2})" -f $target, $info.Length, $info.LastWriteTime
        Write-Ok $msg
    }
}

if (-not $DllOnly) {
    $dashRoot = Join-Path $ProjectRoot 'dashboards'
    if (-not (Test-Path -LiteralPath $dashRoot)) {
        Write-Skip "No dashboards\ folder in project."
    } else {
        $templatesRoot = Join-Path $SimHubPath 'DashTemplates'
        if (-not (Test-Path -LiteralPath $templatesRoot)) {
            New-Item -Path $templatesRoot -ItemType Directory -Force | Out-Null
        }

        $excludePatterns = @('*.bak-*', '*.pre*-*', '*.backup-*')

        Get-ChildItem -LiteralPath $dashRoot -Directory | ForEach-Object {
            $dashName = $_.Name
            $srcDir = $_.FullName
            $dstDir = Join-Path $templatesRoot $dashName
            if (-not (Test-Path -LiteralPath $dstDir)) {
                New-Item -Path $dstDir -ItemType Directory -Force | Out-Null
            }

            $copied = 0
            Get-ChildItem -LiteralPath $srcDir -Recurse -File | ForEach-Object {
                $rel = $_.FullName.Substring($srcDir.Length).TrimStart('\','/')
                $skip = $false
                foreach ($pat in $excludePatterns) {
                    if ($_.Name -like $pat) { $skip = $true; break }
                }
                if ($skip) { return }

                $dst = Join-Path $dstDir $rel
                $dstParent = Split-Path -Parent $dst
                if (-not (Test-Path -LiteralPath $dstParent)) {
                    New-Item -Path $dstParent -ItemType Directory -Force | Out-Null
                }
                Copy-Item -LiteralPath $_.FullName -Destination $dst -Force
                $copied++
            }
            $dashMsg = "Dashboard '{0}' -> {1} ({2} files)" -f $dashName, $dstDir, $copied
            Write-Ok $dashMsg
        }
    }
}

Write-Info "Done."
Write-Host ""
Write-Host "  ============================================================" -ForegroundColor Cyan
Write-Host "  RESTART SimHub now to load the new plugin + dashboard." -ForegroundColor Cyan
Write-Host "  ============================================================" -ForegroundColor Cyan
Write-Host ""

if ($StartSimHub) {
    $exe = Join-Path $SimHubPath 'SimHubWPF.exe'
    if (Test-Path -LiteralPath $exe) {
        Write-Info "Launching $exe ..."
        Start-Process -FilePath $exe
        Write-Ok "SimHubWPF started."
    } else {
        Write-Skip "SimHubWPF.exe not found at '$exe' - launch manually."
    }
}

exit 0
