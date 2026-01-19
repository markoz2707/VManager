param(
    [Parameter(Mandatory = $true)]
    [string]$CentralUrl,

    [Parameter(Mandatory = $true)]
    [string]$RegistrationToken,

    [Parameter(Mandatory = $true)]
    [string]$AgentApiUrl,

    [string]$AgentPath = "",
    [string]$HostType = "Hyper-V",
    [string]$ClusterName = "",
    [string]$Tags = ""
)

$ErrorActionPreference = "Stop"

Write-Host "Registering agent with central server..." -ForegroundColor Cyan

$payload = @{
    hostname = $env:COMPUTERNAME
    apiBaseUrl = $AgentApiUrl
    token = $RegistrationToken
    ipAddress = (Get-NetIPAddress -AddressFamily IPv4 -InterfaceAlias "*" | Where-Object { $_.IPAddress -notlike "169.254*" } | Select-Object -First 1).IPAddress
    hyperVVersion = (Get-ItemProperty "HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion").DisplayVersion
    hostType = $HostType
    tags = $Tags
    clusterName = $ClusterName
}

$registerUri = "$CentralUrl/api/agents/register"
Invoke-RestMethod -Method Post -Uri $registerUri -Body ($payload | ConvertTo-Json) -ContentType "application/json"

if ($AgentPath -ne "")
{
    Write-Host "Starting agent from $AgentPath..." -ForegroundColor Cyan
    if (-not (Test-Path $AgentPath))
    {
        throw "AgentPath not found: $AgentPath"
    }

    $exe = Join-Path $AgentPath "HyperV.HyperV.Agent.exe"
    if (Test-Path $exe)
    {
        Start-Process -FilePath $exe -WorkingDirectory $AgentPath
    }
    else
    {
        Write-Warning "Agent executable not found. Provide a published folder in AgentPath."
    }
}
else
{
    Write-Host "AgentPath not provided. Registration complete." -ForegroundColor Yellow
}
