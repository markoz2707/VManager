# Check Remote Machine Status and Start Debugging Session
param(
    [string]$RemoteHost = "192.168.7.63",
    [string]$Username = "administrator",
    [string]$Password = "Zaq1@wsx"
)

Write-Host "Checking remote machine status and starting debugging session..." -ForegroundColor Green

# Create secure credential
$SecurePassword = ConvertTo-SecureString $Password -AsPlainText -Force
$Credential = New-Object System.Management.Automation.PSCredential($Username, $SecurePassword)

try {
    # Establish remote session
    $Session = New-PSSession -ComputerName $RemoteHost -Credential $Credential -ErrorAction Stop
    Write-Host "Remote session established!" -ForegroundColor Green
    
    # Check what's on the remote machine
    Write-Host "Checking remote machine contents..." -ForegroundColor Yellow
    $RemoteContents = Invoke-Command -Session $Session -ScriptBlock {
        $Results = @{}
        
        # Check if C:\HyperV-Agent exists
        if (Test-Path "C:\HyperV-Agent") {
            $Results.HyperVAgentDir = Get-ChildItem "C:\HyperV-Agent" -ErrorAction SilentlyContinue | Select-Object Name, Length, LastWriteTime
        } else {
            $Results.HyperVAgentDir = "Directory does not exist"
        }
        
        # Check for any HyperV processes
        $Results.HyperVProcesses = Get-Process -Name "*HyperV*" -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, StartTime
        
        # Check for any processes on port 8743
        $Results.Port8743 = netstat -an | findstr ":8743"
        
        # Check current directory contents
        $Results.CurrentDir = Get-Location
        $Results.CurrentDirContents = Get-ChildItem -ErrorAction SilentlyContinue | Select-Object Name, Length
        
        return $Results
    }
    
    Write-Host "Remote machine analysis:" -ForegroundColor Cyan
    Write-Host "Current Directory: $($RemoteContents.CurrentDir)" -ForegroundColor White
    
    if ($RemoteContents.HyperVAgentDir -eq "Directory does not exist") {
        Write-Host "C:\HyperV-Agent directory does not exist!" -ForegroundColor Red
    } else {
        Write-Host "C:\HyperV-Agent contents:" -ForegroundColor White
        $RemoteContents.HyperVAgentDir | Format-Table -AutoSize
    }
    
    if ($RemoteContents.HyperVProcesses) {
        Write-Host "HyperV processes found:" -ForegroundColor Green
        $RemoteContents.HyperVProcesses | Format-Table -AutoSize
    } else {
        Write-Host "No HyperV processes running" -ForegroundColor Yellow
    }
    
    if ($RemoteContents.Port8743) {
        Write-Host "Port 8743 status:" -ForegroundColor Green
        $RemoteContents.Port8743
    } else {
        Write-Host "Port 8743 is not in use" -ForegroundColor Yellow
    }
    
    # Now let's redeploy and start debugging
    Write-Host "`nRedeploying application..." -ForegroundColor Green
    
    # First, build and publish the application locally
    Write-Host "Building application locally..." -ForegroundColor Yellow
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    if ($Session) {
        Remove-PSSession -Session $Session -ErrorAction SilentlyContinue
    }
}
