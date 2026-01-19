<#
.SYNOPSIS
    Updates the HyperV Agent to a new version.

.DESCRIPTION
    This script updates the HyperV Agent by downloading or using a new version,
    stopping the service, replacing binaries, and restarting the service.
    Configuration and logs are preserved.

.PARAMETER SourcePath
    Path to the new agent binaries (published folder)

.PARAMETER BackupPath
    Path where the current version will be backed up (default: C:\ProgramData\HyperV.Agent\backup)

.PARAMETER ServiceName
    Name of the Windows Service (default: HyperV.Agent)

.EXAMPLE
    .\Update-HyperV-Agent.ps1 -SourcePath "C:\temp\hyperv-agent-v2.0"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [string]$BackupPath = "C:\ProgramData\HyperV.Agent\backup\$(Get-Date -Format 'yyyyMMdd_HHmmss')",

    [string]$ServiceName = "HyperV.Agent"
)

$ErrorActionPreference = "Stop"

# Require Administrator privileges
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

Write-Host "Updating HyperV Agent..." -ForegroundColor Cyan

# Validate source path
if (-not (Test-Path $SourcePath)) {
    Write-Error "Source path not found: $SourcePath"
    exit 1
}

$installPath = "C:\Program Files\HyperV.Agent"

# Check if agent is installed
if (-not (Test-Path $installPath)) {
    Write-Error "HyperV Agent is not installed at: $installPath"
    exit 1
}

# Get current version
$currentExe = Join-Path $installPath "HyperV.Agent.exe"
if (Test-Path $currentExe) {
    $currentVersion = (Get-Item $currentExe).VersionInfo.FileVersion
    Write-Host "Current version: $currentVersion" -ForegroundColor Yellow
} else {
    Write-Warning "Could not determine current version"
}

# Get new version
$newExe = Join-Path $SourcePath "HyperV.Agent.exe"
if (Test-Path $newExe) {
    $newVersion = (Get-Item $newExe).VersionInfo.FileVersion
    Write-Host "New version: $newVersion" -ForegroundColor Green
} else {
    Write-Error "New executable not found at: $newExe"
    exit 1
}

# Stop the service
Write-Host "Stopping service: $ServiceName" -ForegroundColor Yellow
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    Stop-Service -Name $ServiceName -Force -ErrorAction Stop
    Start-Sleep -Seconds 2
    Write-Host "Service stopped" -ForegroundColor Green
} else {
    Write-Warning "Service not found: $ServiceName. Continuing with file replacement."
}

# Create backup directory
Write-Host "Creating backup at: $BackupPath" -ForegroundColor Yellow
New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null

# Backup current installation
Write-Host "Backing up current installation..." -ForegroundColor Yellow
Copy-Item -Path $installPath\* -Destination $BackupPath -Recurse -Force
Write-Host "Backup completed" -ForegroundColor Green

# Remove old binaries (preserve logs and config if they're in the install path)
Write-Host "Removing old binaries..." -ForegroundColor Yellow
Get-ChildItem -Path $installPath -Exclude "logs", "appsettings.json", "appsettings.*.json" | Remove-Item -Recurse -Force

# Copy new binaries
Write-Host "Installing new binaries..." -ForegroundColor Yellow
Copy-Item -Path $SourcePath\* -Destination $installPath -Recurse -Force
Write-Host "New binaries installed" -ForegroundColor Green

# Start the service
if ($service) {
    Write-Host "Starting service: $ServiceName" -ForegroundColor Yellow
    Start-Service -Name $ServiceName -ErrorAction Stop
    Start-Sleep -Seconds 3

    $service = Get-Service -Name $ServiceName
    if ($service.Status -eq "Running") {
        Write-Host "Service started successfully" -ForegroundColor Green
    } else {
        Write-Error "Service failed to start. Status: $($service.Status)"
        Write-Host "To rollback, run: Copy-Item -Path '$BackupPath\*' -Destination '$installPath' -Recurse -Force" -ForegroundColor Red
        exit 1
    }
}

# Verify health
Write-Host "`nVerifying agent health..." -ForegroundColor Cyan
Start-Sleep -Seconds 5

$healthUrl = "http://localhost:8743/api/v1/health"
try {
    $response = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 10
    if ($response.status -eq "ok") {
        Write-Host "Health check passed!" -ForegroundColor Green
    } else {
        Write-Warning "Health check returned unexpected response: $($response | ConvertTo-Json)"
    }
} catch {
    Write-Warning "Health check failed: $_"
    Write-Host "The service may still be starting up. Please verify manually." -ForegroundColor Yellow
}

Write-Host "`nUpdate completed successfully!" -ForegroundColor Green
Write-Host "Backup location: $BackupPath" -ForegroundColor Cyan
Write-Host "To rollback: Copy-Item -Path '$BackupPath\*' -Destination '$installPath' -Recurse -Force" -ForegroundColor Cyan
