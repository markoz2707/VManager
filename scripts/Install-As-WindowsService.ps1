<#
.SYNOPSIS
    Installs HyperV Agent as a Windows Service.

.DESCRIPTION
    This script installs the HyperV Agent as a Windows Service using sc.exe.
    It creates the service, configures it for automatic start, and starts it.

.PARAMETER InstallPath
    Path where the agent is installed (default: C:\Program Files\HyperV.Agent)

.PARAMETER ServiceName
    Name of the Windows Service (default: HyperV.Agent)

.PARAMETER DisplayName
    Display name of the service (default: HyperV Agent Service)

.PARAMETER Description
    Service description

.PARAMETER StartType
    Service start type: auto, demand, disabled (default: auto)

.EXAMPLE
    .\Install-As-WindowsService.ps1

.EXAMPLE
    .\Install-As-WindowsService.ps1 -InstallPath "D:\Apps\HyperV.Agent" -ServiceName "HyperVAgent"
#>

param(
    [string]$InstallPath = "C:\Program Files\HyperV.Agent",
    [string]$ServiceName = "HyperV.Agent",
    [string]$DisplayName = "HyperV Agent Service",
    [string]$Description = "REST API service for Hyper-V host management",
    [ValidateSet("auto", "demand", "disabled")]
    [string]$StartType = "auto"
)

$ErrorActionPreference = "Stop"

# Require Administrator privileges
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

Write-Host "Installing HyperV Agent as Windows Service..." -ForegroundColor Cyan

# Validate installation path
$exePath = Join-Path $InstallPath "HyperV.Agent.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "Agent executable not found at: $exePath"
    Write-Host "Please ensure the agent is published to this location first." -ForegroundColor Red
    exit 1
}

# Check if service already exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Service already exists: $ServiceName" -ForegroundColor Yellow
    $response = Read-Host "Do you want to remove and reinstall? (Y/N)"
    if ($response -eq "Y" -or $response -eq "y") {
        Write-Host "Stopping service..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue

        Write-Host "Removing service..." -ForegroundColor Yellow
        sc.exe delete $ServiceName
        Start-Sleep -Seconds 2
    } else {
        Write-Host "Installation cancelled" -ForegroundColor Yellow
        exit 0
    }
}

# Create the service
Write-Host "Creating service: $ServiceName" -ForegroundColor Yellow
sc.exe create $ServiceName binPath= "`"$exePath`"" start= $StartType DisplayName= "$DisplayName"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create service. Error code: $LASTEXITCODE"
    exit 1
}

# Set description
sc.exe description $ServiceName "$Description"

# Configure service recovery options (restart on failure)
Write-Host "Configuring service recovery options..." -ForegroundColor Yellow
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000

# Set service to run as LocalSystem (can be changed to a service account)
# sc.exe config $ServiceName obj= "NT AUTHORITY\LocalSystem"

Write-Host "Service created successfully" -ForegroundColor Green

# Start the service
Write-Host "Starting service..." -ForegroundColor Yellow
Start-Service -Name $ServiceName

Start-Sleep -Seconds 3

# Verify service status
$service = Get-Service -Name $ServiceName
if ($service.Status -eq "Running") {
    Write-Host "Service is running!" -ForegroundColor Green
} else {
    Write-Warning "Service status: $($service.Status)"
    Write-Host "Check Windows Event Viewer for errors" -ForegroundColor Yellow
}

# Create firewall rule
Write-Host "`nConfiguring firewall..." -ForegroundColor Yellow
$firewallRule = Get-NetFirewallRule -DisplayName "HyperV Agent" -ErrorAction SilentlyContinue
if ($firewallRule) {
    Write-Host "Firewall rule already exists" -ForegroundColor Yellow
} else {
    New-NetFirewallRule -DisplayName "HyperV Agent" `
        -Direction Inbound `
        -Protocol TCP `
        -LocalPort 8743 `
        -Action Allow `
        -Profile Domain,Private `
        -Description "Allow inbound traffic to HyperV Agent API"
    Write-Host "Firewall rule created" -ForegroundColor Green
}

Write-Host "`nInstallation completed successfully!" -ForegroundColor Green
Write-Host "`nService Information:" -ForegroundColor Cyan
Write-Host "  Name: $ServiceName"
Write-Host "  Display Name: $DisplayName"
Write-Host "  Status: $($service.Status)"
Write-Host "  Start Type: $($service.StartType)"
Write-Host "  Executable: $exePath"

Write-Host "`nUseful Commands:" -ForegroundColor Cyan
Write-Host "  Start service:   Start-Service -Name $ServiceName"
Write-Host "  Stop service:    Stop-Service -Name $ServiceName"
Write-Host "  Restart service: Restart-Service -Name $ServiceName"
Write-Host "  Check status:    Get-Service -Name $ServiceName"
Write-Host "  View logs:       Get-Content 'C:\ProgramData\HyperV.Agent\logs\hyperv-agent*.log' -Tail 50"
