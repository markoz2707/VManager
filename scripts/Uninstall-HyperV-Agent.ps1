<#
.SYNOPSIS
    Uninstalls the HyperV Agent from a Windows host.

.DESCRIPTION
    This script stops and removes the HyperV Agent Windows Service,
    removes the agent files, and optionally removes configuration and logs.

.PARAMETER RemoveConfig
    Remove configuration files (appsettings.json, certificates)

.PARAMETER RemoveLogs
    Remove log files

.PARAMETER ServiceName
    Name of the Windows Service (default: HyperV.Agent)

.EXAMPLE
    .\Uninstall-HyperV-Agent.ps1

.EXAMPLE
    .\Uninstall-HyperV-Agent.ps1 -RemoveConfig -RemoveLogs
#>

param(
    [switch]$RemoveConfig = $false,
    [switch]$RemoveLogs = $false,
    [string]$ServiceName = "HyperV.Agent"
)

$ErrorActionPreference = "Stop"

# Require Administrator privileges
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

Write-Host "Uninstalling HyperV Agent..." -ForegroundColor Cyan

# Stop the service if running
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Stopping service: $ServiceName" -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue

    Write-Host "Removing service: $ServiceName" -ForegroundColor Yellow
    sc.exe delete $ServiceName

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Service removed successfully" -ForegroundColor Green
    } else {
        Write-Warning "Failed to remove service. Error code: $LASTEXITCODE"
    }
} else {
    Write-Host "Service not found: $ServiceName" -ForegroundColor Yellow
}

# Remove installation directory
$installPath = "C:\Program Files\HyperV.Agent"
if (Test-Path $installPath) {
    Write-Host "Removing installation directory: $installPath" -ForegroundColor Yellow
    Remove-Item -Path $installPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Installation directory removed" -ForegroundColor Green
} else {
    Write-Host "Installation directory not found" -ForegroundColor Yellow
}

# Remove configuration if requested
if ($RemoveConfig) {
    $configPath = "C:\ProgramData\HyperV.Agent"
    if (Test-Path $configPath) {
        Write-Host "Removing configuration directory: $configPath" -ForegroundColor Yellow

        # Backup certificates before removing
        $certsPath = Join-Path $configPath "certs"
        if (Test-Path $certsPath) {
            $backupPath = "C:\ProgramData\HyperV.Agent.Backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
            Write-Host "Backing up certificates to: $backupPath" -ForegroundColor Cyan
            New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
            Copy-Item -Path $certsPath -Destination $backupPath -Recurse
        }

        Remove-Item -Path $configPath -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Configuration directory removed" -ForegroundColor Green
    }
}

# Remove logs if requested
if ($RemoveLogs) {
    $logsPath = "C:\ProgramData\HyperV.Agent\logs"
    if (Test-Path $logsPath) {
        Write-Host "Removing logs directory: $logsPath" -ForegroundColor Yellow
        Remove-Item -Path $logsPath -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Logs directory removed" -ForegroundColor Green
    }
}

# Remove firewall rule if exists
$firewallRule = Get-NetFirewallRule -DisplayName "HyperV Agent" -ErrorAction SilentlyContinue
if ($firewallRule) {
    Write-Host "Removing firewall rule" -ForegroundColor Yellow
    Remove-NetFirewallRule -DisplayName "HyperV Agent"
    Write-Host "Firewall rule removed" -ForegroundColor Green
}

Write-Host "`nUninstallation completed successfully!" -ForegroundColor Green
Write-Host "Note: If you backed up certificates, they are located in C:\ProgramData\HyperV.Agent.Backup_*" -ForegroundColor Cyan
