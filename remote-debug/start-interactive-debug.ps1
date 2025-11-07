# Start Interactive Remote Debugging Session for HyperV Agent
param(
    [string]$RemoteHost = "192.168.7.63",
    [string]$Username = "administrator",
    [string]$Password = "Zaq1@wsx"
)

Write-Host "Starting Interactive Remote Debugging Session..." -ForegroundColor Green
Write-Host "Remote Host: $RemoteHost" -ForegroundColor Yellow

# Create secure credential
$SecurePassword = ConvertTo-SecureString $Password -AsPlainText -Force
$Credential = New-Object System.Management.Automation.PSCredential($Username, $SecurePassword)

try {
    # Establish remote session
    Write-Host "Establishing PowerShell remote session..." -ForegroundColor Yellow
    $Session = New-PSSession -ComputerName $RemoteHost -Credential $Credential -ErrorAction Stop
    Write-Host "Remote session established successfully!" -ForegroundColor Green
    
    # Start the application in debug mode
    Write-Host "Starting HyperV Agent in debug mode..." -ForegroundColor Yellow
    $StartResult = Invoke-Command -Session $Session -ScriptBlock {
        # Change to application directory
        Set-Location "C:\HyperV-Agent"
        
        # Kill any existing processes
        Get-Process -Name "*HyperV*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        
        # Start application in background with debug configuration
        $ProcessInfo = Start-Process -FilePath ".\HyperV.HyperV.Agent.exe" -ArgumentList "--environment=Development", "--urls=http://0.0.0.0:8743" -PassThru -WindowStyle Hidden
        
        # Wait for startup
        Start-Sleep -Seconds 5
        
        # Check if process is still running
        $RunningProcess = Get-Process -Id $ProcessInfo.Id -ErrorAction SilentlyContinue
        
        if ($RunningProcess) {
            # Test if application is responding
            try {
                $Response = Invoke-WebRequest -Uri "http://localhost:8743/health" -TimeoutSec 10 -ErrorAction Stop
                $HealthStatus = @{
                    Success = $true
                    StatusCode = $Response.StatusCode
                    Content = $Response.Content
                }
            } catch {
                $HealthStatus = @{
                    Success = $false
                    Error = $_.Exception.Message
                }
            }
            
            return @{
                ProcessId = $ProcessInfo.Id
                ProcessName = $ProcessInfo.ProcessName
                StartTime = $RunningProcess.StartTime
                WorkingSet = [math]::Round($RunningProcess.WorkingSet / 1MB, 2)
                Health = $HealthStatus
            }
        } else {
            return @{
                Error = "Process failed to start or exited immediately"
            }
        }
    }
    
    if ($StartResult.Error) {
        Write-Host "Failed to start application: $($StartResult.Error)" -ForegroundColor Red
        return
    }
    
    Write-Host "HyperV Agent started successfully!" -ForegroundColor Green
    Write-Host "  Process ID: $($StartResult.ProcessId)" -ForegroundColor Cyan
    Write-Host "  Start Time: $($StartResult.StartTime)" -ForegroundColor Cyan
    Write-Host "  Memory Usage: $($StartResult.WorkingSet) MB" -ForegroundColor Cyan
    
    if ($StartResult.Health.Success) {
        Write-Host "  Health Check: PASSED (Status: $($StartResult.Health.StatusCode))" -ForegroundColor Green
    } else {
        Write-Host "  Health Check: FAILED ($($StartResult.Health.Error))" -ForegroundColor Yellow
    }
    
    Write-Host "`n=== REMOTE DEBUGGING SESSION ACTIVE ===" -ForegroundColor Green
    Write-Host "Application URL: http://192.168.7.63:8743" -ForegroundColor Cyan
    Write-Host "Swagger UI: http://192.168.7.63:8743/swagger" -ForegroundColor Cyan
    
    Write-Host "`nAvailable debugging commands:" -ForegroundColor Yellow
    Write-Host "  Get-Process *HyperV*                                    # Check process status" -ForegroundColor Cyan
    Write-Host "  Get-Content C:\HyperV-Agent\logs\hyperv-agent-remote.log -Tail 20  # View logs" -ForegroundColor Cyan
    Write-Host "  Invoke-WebRequest http://localhost:8743/api/vms         # Test API endpoint" -ForegroundColor Cyan
    Write-Host "  Invoke-WebRequest http://localhost:8743/health          # Health check" -ForegroundColor Cyan
    Write-Host "  netstat -an | findstr 8743                             # Check port status" -ForegroundColor Cyan
    Write-Host "  Stop-Process -Name 'HyperV.HyperV.Agent' -Force        # Stop application" -ForegroundColor Cyan
    
    Write-Host "`nEntering interactive remote debugging session..." -ForegroundColor Green
    Write-Host "Type 'exit' to end the debugging session." -ForegroundColor Yellow
    Write-Host "=========================================" -ForegroundColor Green
    
    # Enter interactive session
    Enter-PSSession -Session $Session
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please ensure WinRM is properly configured." -ForegroundColor Yellow
} finally {
    # Clean up session
    if ($Session) {
        Write-Host "`nCleaning up remote session..." -ForegroundColor Yellow
        Remove-PSSession -Session $Session -ErrorAction SilentlyContinue
        Write-Host "Remote debugging session ended." -ForegroundColor Green
    }
}

Write-Host "`nDebugging session completed." -ForegroundColor Green
