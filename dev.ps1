<#
.SYNOPSIS
    Starts VManager in development mode (API + React frontend).

.DESCRIPTION
    Launches the HyperV.Agent backend (port 8743) and the React dev server
    (port 3000) side by side.  Press Ctrl+C to stop both processes.

.PARAMETER BackendOnly
    Start only the .NET backend without the React dev server.

.PARAMETER FrontendOnly
    Start only the React dev server without the .NET backend.

.PARAMETER NoBrowser
    Do not open the browser automatically.
#>
param(
    [switch]$BackendOnly,
    [switch]$FrontendOnly,
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$agentDir   = Join-Path $root 'src\HyperV.Agent'
$frontendDir = Join-Path $root 'src\HyperV.LocalManagement'

Write-Host ""
Write-Host "  VManager - Development Mode" -ForegroundColor Cyan
Write-Host "  ===========================" -ForegroundColor DarkCyan
Write-Host ""

# --- Backend ---
$backendJob = $null
if (-not $FrontendOnly) {
    Write-Host "  [Backend]  dotnet run  ->  https://localhost:8743" -ForegroundColor Green
    $backendJob = Start-Job -Name 'VManager-Backend' -ScriptBlock {
        param($dir)
        Set-Location $dir
        & dotnet run --no-launch-profile
    } -ArgumentList $agentDir
}

# --- Frontend ---
$frontendJob = $null
if (-not $BackendOnly) {
    # Ensure node_modules exist
    if (-not (Test-Path (Join-Path $frontendDir 'node_modules'))) {
        Write-Host "  [Frontend] Installing dependencies..." -ForegroundColor Yellow
        Push-Location $frontendDir
        & npm install
        Pop-Location
    }

    Write-Host "  [Frontend] vite dev   ->  http://localhost:3000" -ForegroundColor Green
    $frontendJob = Start-Job -Name 'VManager-Frontend' -ScriptBlock {
        param($dir)
        Set-Location $dir
        & npx vite --host
    } -ArgumentList $frontendDir
}

Write-Host ""
Write-Host "  Press Ctrl+C to stop all services." -ForegroundColor DarkGray
Write-Host ""

# Open browser after a short delay
if (-not $NoBrowser -and -not $BackendOnly) {
    Start-Job -Name 'VManager-Browser' -ScriptBlock {
        Start-Sleep -Seconds 4
        Start-Process 'http://localhost:3000'
    } | Out-Null
}

# Stream output from both jobs until Ctrl+C
try {
    while ($true) {
        if ($backendJob) {
            Receive-Job -Job $backendJob -ErrorAction SilentlyContinue | ForEach-Object {
                Write-Host "  [API] $_" -ForegroundColor DarkGreen
            }
        }
        if ($frontendJob) {
            Receive-Job -Job $frontendJob -ErrorAction SilentlyContinue | ForEach-Object {
                Write-Host "  [UI]  $_" -ForegroundColor DarkCyan
            }
        }
        Start-Sleep -Milliseconds 500
    }
}
finally {
    Write-Host ""
    Write-Host "  Stopping services..." -ForegroundColor Yellow
    Get-Job -Name 'VManager-*' -ErrorAction SilentlyContinue | Stop-Job -PassThru | Remove-Job
    Write-Host "  Done." -ForegroundColor Green
}
