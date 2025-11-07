# WinRM Setup Script for Remote Debugging
# Run this script as Administrator on the LOCAL machine

param(
    [string]$RemoteHost = "192.168.7.63"
)

Write-Host "Setting up WinRM for remote debugging to $RemoteHost" -ForegroundColor Green

# Check if running as administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Please right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    exit 1
}

try {
    # Enable WinRM on local machine
    Write-Host "Enabling WinRM on local machine..." -ForegroundColor Cyan
    Enable-PSRemoting -Force -SkipNetworkProfileCheck

    # Add remote host to TrustedHosts
    Write-Host "Adding $RemoteHost to TrustedHosts..." -ForegroundColor Cyan
    $currentTrustedHosts = Get-Item WSMan:\localhost\Client\TrustedHosts -ErrorAction SilentlyContinue
    
    if ($currentTrustedHosts.Value -eq "" -or $currentTrustedHosts.Value -eq $null) {
        Set-Item WSMan:\localhost\Client\TrustedHosts -Value $RemoteHost -Force
    } elseif ($currentTrustedHosts.Value -notlike "*$RemoteHost*") {
        Set-Item WSMan:\localhost\Client\TrustedHosts -Value "$($currentTrustedHosts.Value),$RemoteHost" -Force
    }

    # Configure WinRM settings
    Write-Host "Configuring WinRM settings..." -ForegroundColor Cyan
    winrm set winrm/config/client '@{AllowUnencrypted="true"}'
    winrm set winrm/config/service '@{AllowUnencrypted="true"}'
    winrm set winrm/config/service/auth '@{Basic="true"}'
    winrm set winrm/config/client/auth '@{Basic="true"}'

    # Test WinRM configuration
    Write-Host "Testing WinRM configuration..." -ForegroundColor Cyan
    winrm enumerate winrm/config/listener

    Write-Host "✓ WinRM setup completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "IMPORTANT: You also need to configure WinRM on the REMOTE machine ($RemoteHost):" -ForegroundColor Yellow
    Write-Host "1. Log into $RemoteHost as administrator" -ForegroundColor White
    Write-Host "2. Run PowerShell as Administrator" -ForegroundColor White
    Write-Host "3. Execute these commands:" -ForegroundColor White
    Write-Host "   Enable-PSRemoting -Force" -ForegroundColor Cyan
    Write-Host "   Set-Item WSMan:\localhost\Client\TrustedHosts -Value '*' -Force" -ForegroundColor Cyan
    Write-Host "   winrm set winrm/config/service '@{AllowUnencrypted=`"true`"}'" -ForegroundColor Cyan
    Write-Host "   winrm set winrm/config/service/auth '@{Basic=`"true`"}'" -ForegroundColor Cyan
    Write-Host "   New-NetFirewallRule -DisplayName 'WinRM HTTP' -Direction Inbound -Protocol TCP -LocalPort 5985 -Action Allow" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "After configuring the remote machine, test the connection with:" -ForegroundColor Yellow
    Write-Host "   .\simple-test.ps1" -ForegroundColor Cyan

} catch {
    Write-Host "✗ Error setting up WinRM: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Make sure you're running PowerShell as Administrator" -ForegroundColor Yellow
}
