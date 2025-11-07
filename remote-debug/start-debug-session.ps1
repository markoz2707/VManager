# Start Remote Debugging Session for HyperV Agent
# This script initiates an actual debugging session on the remote machine

param(
    [string]$RemoteHost = "192.168.7.63",
    [string]$Username = "administrator",
    [string]$Password = "Zaq1@wsx"
)

Write-Host "Starting Remote Debugging Session for HyperV Agent..." -ForegroundColor Green
Write-Host "Remote Host: $RemoteHost" -ForegroundColor Yellow

# Create secure credential
$SecurePassword = ConvertTo-SecureString $Password -AsPlainText -Force
$Credential = New-Object System.Management.Automation.PSCredential($Username, $SecurePassword)

try {
    # Establish remote session
    Write-Host "Establishing PowerShell remote session..." -ForegroundColor Yellow
    $Session = New-PSSession -ComputerName $RemoteHost -Credential $Credential -ErrorAction Stop
    
    Write-Host "Remote session established successfully!" -ForegroundColor Green
    
    # Check if HyperV Agent is running
    Write-Host "Checking HyperV Agent status..." -ForegroundColor Yellow
    $ProcessInfo = Invoke-Command -Session $Session -ScriptBlock {
        Get-Process -Name "HyperV.Agent" -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, StartTime, WorkingSet
    }
    
    if ($ProcessInfo) {
        Write-Host "HyperV Agent is running:" -ForegroundColor Green
        Write-Host "  Process ID: $($ProcessInfo.Id)" -ForegroundColor Cyan
        Write-Host "  Start Time: $($ProcessInfo.StartTime)" -ForegroundColor Cyan
        Write-Host "  Working Set: $([math]::Round($ProcessInfo.WorkingSet / 1MB, 2)) MB" -ForegroundColor Cyan
        
        # Check if application is responding
        Write-Host "Testing application endpoint..." -ForegroundColor Yellow
        $EndpointTest = Invoke-Command -Session $Session -ScriptBlock {
            try {
                $Response = Invoke-WebRequest -Uri "http://localhost:8743/health" -TimeoutSec 10 -ErrorAction Stop
                return @{
                    Success = $true
                    StatusCode = $Response.StatusCode
                    Content = $Response.Content
                }
            } catch {
                return @{
                    Success = $false
                    Error = $_.Exception.Message
                }
            }
        }
        
        if ($EndpointTest.Success) {
            Write-Host "Application is responding on port 8743!" -ForegroundColor Green
            Write-Host "Status Code: $($EndpointTest.StatusCode)" -ForegroundColor Cyan
        } else {
            Write-Host "Application endpoint test failed: $($EndpointTest.Error)" -ForegroundColor Red
        }
        
        # Start debugging session
        Write-Host "`nStarting interactive debugging session..." -ForegroundColor Green
        Write-Host "You can now use the following debugging commands:" -ForegroundColor Yellow
        Write-Host "  - Get-Process HyperV.Agent" -ForegroundColor Cyan
        Write-Host "  - Get-EventLog -LogName Application -Source HyperV.Agent -Newest 10" -ForegroundColor Cyan
        Write-Host "  - Get-Content C:\HyperV-Agent\logs\hyperv-agent-remote.log -Tail 20" -ForegroundColor Cyan
        Write-Host "  - Invoke-WebRequest http://localhost:8743/api/vms" -ForegroundColor Cyan
        Write-Host "  - netstat -an | findstr 8743" -ForegroundColor Cyan
        
        Write-Host "`nEntering interactive remote session..." -ForegroundColor Green
        Write-Host "Type 'exit' to end the debugging session." -ForegroundColor Yellow
        
        # Enter interactive session
        Enter-PSSession -Session $Session
        
    } else {
        Write-Host "HyperV Agent is not running. Starting application in debug mode..." -ForegroundColor Yellow
        
        # Start application in debug mode
        $StartResult = Invoke-Command -Session $Session -ScriptBlock {
            Set-Location "C:\HyperV-Agent"
            
            # Start application with debug configuration
            $Process = Start-Process -FilePath ".\HyperV.Agent.exe" -ArgumentList "--environment=Development" -PassThru -WindowStyle Hidden
            
            # Wait a moment for startup
            Start-Sleep -Seconds 3
            
            return @{
                ProcessId = $Process.Id
                ProcessName = $Process.ProcessName
            }
        }
        
        Write-Host "Application started in debug mode!" -ForegroundColor Green
        Write-Host "Process ID: $($StartResult.ProcessId)" -ForegroundColor Cyan
        
        # Enter interactive session for debugging
        Write-Host "Entering interactive debugging session..." -ForegroundColor Green
        Enter-PSSession -Session $Session
    }
    
} catch {
    Write-Host "Error establishing remote session: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please ensure:" -ForegroundColor Yellow
    Write-Host "  1. WinRM is enabled on the remote machine" -ForegroundColor Cyan
    Write-Host "  2. Remote machine is in TrustedHosts" -ForegroundColor Cyan
    Write-Host "  3. Credentials are correct" -ForegroundColor Cyan
    Write-Host "  4. Firewall allows WinRM (port 5985)" -ForegroundColor Cyan
} finally {
    # Clean up session
    if ($Session) {
        Remove-PSSession -Session $Session -ErrorAction SilentlyContinue
        Write-Host "Remote session closed." -ForegroundColor Yellow
    }
}

Write-Host "Remote debugging session ended." -ForegroundColor Green
