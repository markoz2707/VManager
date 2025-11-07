# Test Remote Debugging Capabilities
param(
    [string]$RemoteHost = "192.168.7.63",
    [string]$Username = "administrator",
    [string]$Password = "Zaq1@wsx"
)

Write-Host "Testing Remote Debugging Capabilities..." -ForegroundColor Green

# Create secure credential
$SecurePassword = ConvertTo-SecureString $Password -AsPlainText -Force
$Credential = New-Object System.Management.Automation.PSCredential($Username, $SecurePassword)

try {
    # Establish remote session
    $Session = New-PSSession -ComputerName $RemoteHost -Credential $Credential -ErrorAction Stop
    Write-Host "Remote session established!" -ForegroundColor Green
    
    # Test debugging capabilities
    $DebugResults = Invoke-Command -Session $Session -ScriptBlock {
        $Results = @{}
        
        # 1. Check process status
        Write-Host "1. Checking HyperV Agent process..." -ForegroundColor Yellow
        $Process = Get-Process -Name "*HyperV*" -ErrorAction SilentlyContinue
        if ($Process) {
            $Results.ProcessStatus = @{
                Success = $true
                ProcessId = $Process.Id
                ProcessName = $Process.ProcessName
                StartTime = $Process.StartTime
                WorkingSet = [math]::Round($Process.WorkingSet / 1MB, 2)
                CPU = $Process.CPU
            }
            Write-Host "   ✓ Process running (PID: $($Process.Id), Memory: $([math]::Round($Process.WorkingSet / 1MB, 2)) MB)" -ForegroundColor Green
        } else {
            $Results.ProcessStatus = @{ Success = $false; Error = "No HyperV process found" }
            Write-Host "   ✗ No HyperV process found" -ForegroundColor Red
        }
        
        # 2. Test local health endpoint
        Write-Host "2. Testing health endpoint locally..." -ForegroundColor Yellow
        try {
            $HealthResponse = Invoke-WebRequest -Uri "http://localhost:8743/health" -TimeoutSec 5 -ErrorAction Stop
            $Results.HealthCheck = @{
                Success = $true
                StatusCode = $HealthResponse.StatusCode
                Content = $HealthResponse.Content
            }
            Write-Host "   ✓ Health check passed (Status: $($HealthResponse.StatusCode))" -ForegroundColor Green
        } catch {
            $Results.HealthCheck = @{
                Success = $false
                Error = $_.Exception.Message
            }
            Write-Host "   ✗ Health check failed: $($_.Exception.Message)" -ForegroundColor Red
        }
        
        # 3. Test API endpoint
        Write-Host "3. Testing API endpoint..." -ForegroundColor Yellow
        try {
            $ApiResponse = Invoke-WebRequest -Uri "http://localhost:8743/api/vms" -TimeoutSec 5 -ErrorAction Stop
            $Results.ApiTest = @{
                Success = $true
                StatusCode = $ApiResponse.StatusCode
                ContentLength = $ApiResponse.Content.Length
            }
            Write-Host "   ✓ API endpoint accessible (Status: $($ApiResponse.StatusCode), Content: $($ApiResponse.Content.Length) bytes)" -ForegroundColor Green
        } catch {
            $Results.ApiTest = @{
                Success = $false
                Error = $_.Exception.Message
            }
            Write-Host "   ✗ API test failed: $($_.Exception.Message)" -ForegroundColor Red
        }
        
        # 4. Check port binding
        Write-Host "4. Checking port 8743 binding..." -ForegroundColor Yellow
        $PortCheck = netstat -an | findstr ":8743"
        if ($PortCheck) {
            $Results.PortBinding = @{
                Success = $true
                Bindings = $PortCheck
            }
            Write-Host "   ✓ Port 8743 is bound:" -ForegroundColor Green
            $PortCheck | ForEach-Object { Write-Host "     $_" -ForegroundColor Cyan }
        } else {
            $Results.PortBinding = @{
                Success = $false
                Error = "Port 8743 not found in netstat output"
            }
            Write-Host "   ✗ Port 8743 not bound" -ForegroundColor Red
        }
        
        # 5. Check firewall rules
        Write-Host "5. Checking firewall rules..." -ForegroundColor Yellow
        try {
            $FirewallRules = Get-NetFirewallRule -DisplayName "*HyperV*" -ErrorAction SilentlyContinue
            if ($FirewallRules) {
                $Results.FirewallRules = @{
                    Success = $true
                    Rules = $FirewallRules | Select-Object DisplayName, Enabled, Direction, Action
                }
                Write-Host "   ✓ Found HyperV firewall rules:" -ForegroundColor Green
                $FirewallRules | ForEach-Object { 
                    Write-Host "     $($_.DisplayName) - $($_.Direction) - $($_.Action) - Enabled: $($_.Enabled)" -ForegroundColor Cyan 
                }
            } else {
                $Results.FirewallRules = @{
                    Success = $false
                    Error = "No HyperV firewall rules found"
                }
                Write-Host "   ⚠ No HyperV firewall rules found" -ForegroundColor Yellow
            }
        } catch {
            $Results.FirewallRules = @{
                Success = $false
                Error = $_.Exception.Message
            }
            Write-Host "   ✗ Error checking firewall: $($_.Exception.Message)" -ForegroundColor Red
        }
        
        # 6. Create firewall rule for external access
        Write-Host "6. Creating firewall rule for external access..." -ForegroundColor Yellow
        try {
            # Remove existing rule if it exists
            Remove-NetFirewallRule -DisplayName "HyperV Agent HTTP" -ErrorAction SilentlyContinue
            
            # Create new rule
            New-NetFirewallRule -DisplayName "HyperV Agent HTTP" -Direction Inbound -Protocol TCP -LocalPort 8743 -Action Allow -ErrorAction Stop
            $Results.FirewallRuleCreation = @{
                Success = $true
                Message = "Firewall rule created successfully"
            }
            Write-Host "   ✓ Firewall rule created for port 8743" -ForegroundColor Green
        } catch {
            $Results.FirewallRuleCreation = @{
                Success = $false
                Error = $_.Exception.Message
            }
            Write-Host "   ✗ Failed to create firewall rule: $($_.Exception.Message)" -ForegroundColor Red
        }
        
        # 7. Check application logs
        Write-Host "7. Checking application logs..." -ForegroundColor Yellow
        $LogPath = "C:\HyperV-Agent\logs\hyperv-agent-remote.log"
        if (Test-Path $LogPath) {
            try {
                $LogContent = Get-Content $LogPath -Tail 10 -ErrorAction Stop
                $Results.ApplicationLogs = @{
                    Success = $true
                    LogPath = $LogPath
                    RecentEntries = $LogContent
                }
                Write-Host "   ✓ Application logs found. Recent entries:" -ForegroundColor Green
                $LogContent | ForEach-Object { Write-Host "     $_" -ForegroundColor Cyan }
            } catch {
                $Results.ApplicationLogs = @{
                    Success = $false
                    Error = $_.Exception.Message
                }
                Write-Host "   ✗ Error reading logs: $($_.Exception.Message)" -ForegroundColor Red
            }
        } else {
            $Results.ApplicationLogs = @{
                Success = $false
                Error = "Log file not found at $LogPath"
            }
            Write-Host "   ⚠ Log file not found at $LogPath" -ForegroundColor Yellow
        }
        
        return $Results
    }
    
    # Display summary
    Write-Host "`n=== DEBUGGING CAPABILITIES TEST SUMMARY ===" -ForegroundColor Green
    Write-Host "Process Status: $(if($DebugResults.ProcessStatus.Success){'✓ PASS'}else{'✗ FAIL'})" -ForegroundColor $(if($DebugResults.ProcessStatus.Success){'Green'}else{'Red'})
    Write-Host "Health Check: $(if($DebugResults.HealthCheck.Success){'✓ PASS'}else{'✗ FAIL'})" -ForegroundColor $(if($DebugResults.HealthCheck.Success){'Green'}else{'Red'})
    Write-Host "API Test: $(if($DebugResults.ApiTest.Success){'✓ PASS'}else{'✗ FAIL'})" -ForegroundColor $(if($DebugResults.ApiTest.Success){'Green'}else{'Red'})
    Write-Host "Port Binding: $(if($DebugResults.PortBinding.Success){'✓ PASS'}else{'✗ FAIL'})" -ForegroundColor $(if($DebugResults.PortBinding.Success){'Green'}else{'Red'})
    Write-Host "Firewall Rule: $(if($DebugResults.FirewallRuleCreation.Success){'✓ PASS'}else{'✗ FAIL'})" -ForegroundColor $(if($DebugResults.FirewallRuleCreation.Success){'Green'}else{'Red'})
    Write-Host "Application Logs: $(if($DebugResults.ApplicationLogs.Success){'✓ PASS'}else{'⚠ PARTIAL'})" -ForegroundColor $(if($DebugResults.ApplicationLogs.Success){'Green'}else{'Yellow'})
    
    Write-Host "`nRemote debugging session is active and ready for use!" -ForegroundColor Green
    Write-Host "Application URL: http://192.168.7.63:8743" -ForegroundColor Cyan
    Write-Host "Swagger UI: http://192.168.7.63:8743/swagger" -ForegroundColor Cyan
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    if ($Session) {
        Remove-PSSession -Session $Session -ErrorAction SilentlyContinue
    }
}
