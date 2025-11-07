$RemoteHost = "192.168.7.63"
$Username = "administrator"
$Password = "Zaq1@wsx"

$SecurePassword = ConvertTo-SecureString $Password -AsPlainText -Force
$Credential = New-Object System.Management.Automation.PSCredential($Username, $SecurePassword)

Write-Host "Testing connection to $RemoteHost..." -ForegroundColor Cyan

try {
    $session = New-PSSession -ComputerName $RemoteHost -Credential $Credential -ErrorAction Stop
    Write-Host "Connection successful!" -ForegroundColor Green
    
    $info = Invoke-Command -Session $session -ScriptBlock {
        $os = Get-WmiObject -Class Win32_OperatingSystem
        return @{
            OSName = $os.Caption
            ComputerName = $env:COMPUTERNAME
            DotNetInstalled = Test-Path "C:\Program Files\dotnet\dotnet.exe"
        }
    }
    
    Write-Host "OS: $($info.OSName)" -ForegroundColor White
    Write-Host "Computer: $($info.ComputerName)" -ForegroundColor White
    Write-Host ".NET Core: $(if($info.DotNetInstalled) { 'Installed' } else { 'Not Found' })" -ForegroundColor White
    
    Remove-PSSession $session
}
catch {
    Write-Host "Connection failed: $($_.Exception.Message)" -ForegroundColor Red
}
