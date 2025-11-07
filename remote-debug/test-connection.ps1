# Simple connection test script
param(
    [string]$RemoteHost = "192.168.7.63",
    [string]$Username = "administrator",
    [string]$Password = "Zaq1@wsx"
)

$SecurePassword = ConvertTo-SecureString $Password -AsPlainText -Force
$Credential = New-Object System.Management.Automation.PSCredential($Username, $SecurePassword)

Write-Host "Testing connection to $RemoteHost..." -ForegroundColor Cyan

try {
    $session = New-PSSession -ComputerName $RemoteHost -Credential $Credential -ErrorAction Stop
    Write-Host "✓ Connection successful!" -ForegroundColor Green
    
    # Get basic system info
    $info = Invoke-Command -Session $session -ScriptBlock {
        $os = Get-WmiObject -Class Win32_OperatingSystem
        $computer = Get-WmiObject -Class Win32_ComputerSystem
        
        return @{
            OSName = $os.Caption
            OSVersion = $os.Version
            ComputerName = $computer.Name
            TotalMemory = [math]::Round($computer.TotalPhysicalMemory / 1GB, 2)
            DotNetInstalled = Test-Path "C:\Program Files\dotnet\dotnet.exe"
        }
    }
    
    Write-Host "Remote System Information:" -ForegroundColor Yellow
    Write-Host "  OS: $($info.OSName)" -ForegroundColor White
    Write-Host "  Version: $($info.OSVersion)" -ForegroundColor White
    Write-Host "  Computer: $($info.ComputerName)" -ForegroundColor White
    Write-Host "  Memory: $($info.TotalMemory) GB" -ForegroundColor White
    Write-Host "  .NET Core: $(if($info.DotNetInstalled) { 'Installed' } else { 'Not Found' })" -ForegroundColor White
    
    Remove-PSSession $session
}
catch {
    Write-Host "✗ Connection failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please check:" -ForegroundColor Yellow
    Write-Host "  - Network connectivity to $RemoteHost" -ForegroundColor White
    Write-Host "  - WinRM is enabled on remote machine" -ForegroundColor White
    Write-Host "  - Credentials are correct" -ForegroundColor White
    Write-Host "  - Windows Firewall allows WinRM" -ForegroundColor White
}
