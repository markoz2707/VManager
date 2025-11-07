# Install .NET Core on Remote Machine
# Target: Windows Server 2025 (192.168.7.63)

$RemoteHost = "192.168.7.63"
$Username = "administrator"
$Password = "Zaq1@wsx"

$SecurePassword = ConvertTo-SecureString $Password -AsPlainText -Force
$Credential = New-Object System.Management.Automation.PSCredential($Username, $SecurePassword)

Write-Host "Installing .NET Core on remote machine $RemoteHost..." -ForegroundColor Green

try {
    $session = New-PSSession -ComputerName $RemoteHost -Credential $Credential -ErrorAction Stop
    Write-Host "✓ Connected to remote machine" -ForegroundColor Green
    
    Invoke-Command -Session $session -ScriptBlock {
        Write-Host "Downloading .NET Core installer..." -ForegroundColor Cyan
        
        # Download .NET 8.0 Runtime (ASP.NET Core)
        $dotnetUrl = "https://download.microsoft.com/download/8/4/f/84f64c1d-a4d6-4675-8e46-0e1c8e2b7b7e/dotnet-hosting-8.0.0-win.exe"
        $installerPath = "$env:TEMP\dotnet-hosting-8.0.0-win.exe"
        
        try {
            Invoke-WebRequest -Uri $dotnetUrl -OutFile $installerPath -UseBasicParsing
            Write-Host "✓ Downloaded .NET installer" -ForegroundColor Green
            
            Write-Host "Installing .NET Core..." -ForegroundColor Cyan
            Start-Process -FilePath $installerPath -ArgumentList "/quiet" -Wait
            
            Write-Host "✓ .NET Core installation completed" -ForegroundColor Green
            
            # Verify installation
            if (Test-Path "C:\Program Files\dotnet\dotnet.exe") {
                Write-Host "✓ .NET Core successfully installed" -ForegroundColor Green
                & "C:\Program Files\dotnet\dotnet.exe" --version
            } else {
                Write-Host "⚠ .NET Core installation may have failed" -ForegroundColor Yellow
            }
            
            # Clean up installer
            Remove-Item $installerPath -Force -ErrorAction SilentlyContinue
            
        } catch {
            Write-Host "✗ Error during installation: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    Remove-PSSession $session
    Write-Host "✓ .NET Core installation process completed" -ForegroundColor Green
    
} catch {
    Write-Host "✗ Connection failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Testing .NET installation..." -ForegroundColor Cyan
.\simple-test.ps1
