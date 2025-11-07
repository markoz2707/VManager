# Remote Deployment and Debugging Script for HyperV Agent
# Target: Windows Server 2025 (192.168.7.63)
# Credentials: administrator/Zaq1@wsx

param(
    [string]$RemoteHost = "192.168.7.63",
    [string]$Username = "administrator",
    [string]$Password = "Zaq1@wsx",
    [string]$RemotePath = "C:\HyperV-Agent",
    [switch]$Deploy,
    [switch]$Debug,
    [switch]$Stop,
    [switch]$Logs
)

# Convert password to secure string
$SecurePassword = ConvertTo-SecureString $Password -AsPlainText -Force
$Credential = New-Object System.Management.Automation.PSCredential($Username, $SecurePassword)

Write-Host "HyperV Agent Remote Debugging Tool" -ForegroundColor Green
Write-Host "Target: $RemoteHost" -ForegroundColor Yellow
Write-Host "Remote Path: $RemotePath" -ForegroundColor Yellow

function Test-RemoteConnection {
    Write-Host "Testing connection to $RemoteHost..." -ForegroundColor Cyan
    try {
        $session = New-PSSession -ComputerName $RemoteHost -Credential $Credential -ErrorAction Stop
        Remove-PSSession $session
        Write-Host "✓ Connection successful" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "✗ Connection failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Deploy-Application {
    Write-Host "Deploying application to remote server..." -ForegroundColor Cyan
    
    # Build the application first
    Write-Host "Building application..." -ForegroundColor Yellow
    Set-Location ..
    dotnet build src/HyperV.Agent/HyperV.Agent.csproj -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Build failed" -ForegroundColor Red
        return $false
    }
    
    # Publish the application
    Write-Host "Publishing application..." -ForegroundColor Yellow
    dotnet publish src/HyperV.Agent/HyperV.Agent.csproj -c Release -o publish/
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Publish failed" -ForegroundColor Red
        return $false
    }
    
    try {
        $session = New-PSSession -ComputerName $RemoteHost -Credential $Credential
        
        # Create remote directory
        Invoke-Command -Session $session -ScriptBlock {
            param($path)
            if (Test-Path $path) {
                Write-Host "Stopping existing service..."
                Get-Process -Name "HyperV.Agent" -ErrorAction SilentlyContinue | Stop-Process -Force
                Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*HyperV.Agent*" } | Stop-Process -Force
                Start-Sleep -Seconds 2
            }
            New-Item -ItemType Directory -Path $path -Force | Out-Null
        } -ArgumentList $RemotePath
        
        # Copy files to remote server
        Write-Host "Copying files to remote server..." -ForegroundColor Yellow
        Copy-Item -Path "publish/*" -Destination $RemotePath -ToSession $session -Recurse -Force
        
        # Copy configuration files
        Copy-Item -Path "remote-debug/appsettings.remote.json" -Destination "$RemotePath/appsettings.json" -ToSession $session -Force
        
        Remove-PSSession $session
        Write-Host "✓ Deployment completed" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "✗ Deployment failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Start-RemoteDebugging {
    Write-Host "Starting remote debugging session..." -ForegroundColor Cyan
    
    try {
        $session = New-PSSession -ComputerName $RemoteHost -Credential $Credential
        
        # Start the application with debugging enabled
        Invoke-Command -Session $session -ScriptBlock {
            param($path)
            Set-Location $path
            
            # Configure Windows Firewall for debugging
            Write-Host "Configuring firewall rules..."
            New-NetFirewallRule -DisplayName "HyperV Agent HTTP" -Direction Inbound -Protocol TCP -LocalPort 8743 -Action Allow -ErrorAction SilentlyContinue
            New-NetFirewallRule -DisplayName "HyperV Agent Debug" -Direction Inbound -Protocol TCP -LocalPort 4024 -Action Allow -ErrorAction SilentlyContinue
            
            # Set environment for debugging
            $env:ASPNETCORE_ENVIRONMENT = "Development"
            $env:ASPNETCORE_URLS = "http://0.0.0.0:8743"
            
            Write-Host "Starting HyperV Agent with debugging..."
            $process = Start-Process -FilePath "dotnet" -ArgumentList "HyperV.Agent.dll" -NoNewWindow -PassThru
            
            Write-Host "Application started with PID: $($process.Id). Checking if it's responding..."
            Start-Sleep -Seconds 5
            
            try {
                $response = Invoke-WebRequest -Uri "http://localhost:8743/api/v1/health" -UseBasicParsing -TimeoutSec 10
                Write-Host "✓ Application is responding: $($response.StatusCode)"
            }
            catch {
                Write-Host "⚠ Application may not be fully started yet: $($_.Exception.Message)"
            }
        } -ArgumentList $RemotePath
        
        Remove-PSSession $session
        
        Write-Host "✓ Remote debugging started" -ForegroundColor Green
        Write-Host "Application URL: http://$RemoteHost:8743" -ForegroundColor Yellow
        Write-Host "Swagger UI: http://$RemoteHost:8743/swagger" -ForegroundColor Yellow
        Write-Host "Health Check: http://$RemoteHost:8743/api/v1/health" -ForegroundColor Yellow
        
        return $true
    }
    catch {
        Write-Host "✗ Failed to start remote debugging: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Stop-RemoteApplication {
    Write-Host "Stopping remote application..." -ForegroundColor Cyan
    
    try {
        $session = New-PSSession -ComputerName $RemoteHost -Credential $Credential
        
        Invoke-Command -Session $session -ScriptBlock {
            Get-Process -Name "HyperV.Agent" -ErrorAction SilentlyContinue | Stop-Process -Force
            Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*HyperV.Agent*" } | Stop-Process -Force
            Write-Host "✓ Application stopped"
        }
        
        Remove-PSSession $session
        Write-Host "✓ Remote application stopped" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "✗ Failed to stop remote application: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Get-RemoteLogs {
    Write-Host "Retrieving remote logs..." -ForegroundColor Cyan
    
    try {
        $session = New-PSSession -ComputerName $RemoteHost -Credential $Credential
        
        Invoke-Command -Session $session -ScriptBlock {
            param($path)
            $logPath = Join-Path $path "logs"
            if (Test-Path $logPath) {
                Get-ChildItem $logPath -Filter "*.log" | ForEach-Object {
                    Write-Host "=== $($_.Name) ===" -ForegroundColor Yellow
                    Get-Content $_.FullName -Tail 50
                }
            } else {
                Write-Host "No log files found at $logPath"
            }
            
            # Also check Windows Event Log
            Write-Host "=== Windows Application Event Log (Last 10 entries) ===" -ForegroundColor Yellow
            try {
                Get-WinEvent -LogName Application -MaxEvents 10 | Where-Object { $_.ProviderName -like "*HyperV*" -or $_.ProviderName -like "*dotnet*" } | Format-Table TimeCreated, Id, LevelDisplayName, Message -Wrap
            }
            catch {
                Write-Host "Could not retrieve Windows Event Log: $($_.Exception.Message)"
            }
        } -ArgumentList $RemotePath
        
        Remove-PSSession $session
        Write-Host "✓ Logs retrieved" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "✗ Failed to retrieve logs: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Main execution logic
if (-not (Test-RemoteConnection)) {
    Write-Host "Cannot proceed without remote connection" -ForegroundColor Red
    exit 1
}

if ($Deploy) {
    if (-not (Deploy-Application)) {
        exit 1
    }
}

if ($Debug) {
    if (-not (Start-RemoteDebugging)) {
        exit 1
    }
}

if ($Stop) {
    Stop-RemoteApplication
}

if ($Logs) {
    Get-RemoteLogs
}

if (-not ($Deploy -or $Debug -or $Stop -or $Logs)) {
    Write-Host "Usage:" -ForegroundColor Yellow
    Write-Host "  .\deploy-to-remote.ps1 -Deploy          # Deploy application to remote server"
    Write-Host "  .\deploy-to-remote.ps1 -Debug           # Start remote debugging"
    Write-Host "  .\deploy-to-remote.ps1 -Deploy -Debug   # Deploy and start debugging"
    Write-Host "  .\deploy-to-remote.ps1 -Stop            # Stop remote application"
    Write-Host "  .\deploy-to-remote.ps1 -Logs            # Get remote logs"
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Cyan
    Write-Host "  .\deploy-to-remote.ps1 -Deploy -Debug   # Full deployment and debug"
    Write-Host "  .\deploy-to-remote.ps1 -Logs            # Check application logs"
}
