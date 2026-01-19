<#
.SYNOPSIS
    Builds all VManager components.

.DESCRIPTION
    This script builds the HyperV Agent, Central Management server,
    and Local Management UI in the correct order.

.PARAMETER Configuration
    Build configuration: Debug or Release (default: Release)

.PARAMETER Clean
    Clean before building

.PARAMETER SkipTests
    Skip running tests

.PARAMETER Publish
    Publish ready-to-deploy artifacts

.EXAMPLE
    .\Build-All.ps1

.EXAMPLE
    .\Build-All.ps1 -Configuration Release -Publish
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Clean = $false,
    [switch]$SkipTests = $false,
    [switch]$Publish = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " VManager Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Root Directory: $rootDir" -ForegroundColor Yellow
Write-Host ""

# Clean if requested
if ($Clean) {
    Write-Host "Cleaning solution..." -ForegroundColor Yellow
    dotnet clean "$rootDir\HyperV.sln" --configuration $Configuration
    Write-Host "Clean completed" -ForegroundColor Green
    Write-Host ""
}

# Restore dependencies
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore "$rootDir\HyperV.sln"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Restore failed"
    exit 1
}
Write-Host "Restore completed" -ForegroundColor Green
Write-Host ""

# Build solution
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet build "$rootDir\HyperV.sln" --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}
Write-Host "Build completed" -ForegroundColor Green
Write-Host ""

# Build Local Management UI (React/Vite)
$localMgmtDir = Join-Path $rootDir "src\HyperV.LocalManagement"
if (Test-Path $localMgmtDir) {
    Write-Host "Building Local Management UI..." -ForegroundColor Yellow
    Push-Location $localMgmtDir

    # Install dependencies
    Write-Host "  Installing npm dependencies..." -ForegroundColor Cyan
    npm install
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Write-Error "npm install failed"
        exit 1
    }

    # Build UI
    Write-Host "  Building UI..." -ForegroundColor Cyan
    npm run build
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Write-Error "UI build failed"
        exit 1
    }

    # Copy to Agent wwwroot
    $distDir = Join-Path $localMgmtDir "dist"
    $wwwrootDir = Join-Path $rootDir "src\HyperV.Agent\wwwroot"

    if (Test-Path $distDir) {
        Write-Host "  Copying UI to Agent wwwroot..." -ForegroundColor Cyan
        if (Test-Path $wwwrootDir) {
            Remove-Item -Path $wwwrootDir -Recurse -Force
        }
        Copy-Item -Path $distDir -Destination $wwwrootDir -Recurse -Force
        Write-Host "  UI build completed" -ForegroundColor Green
    } else {
        Write-Warning "Dist directory not found: $distDir"
    }

    Pop-Location
    Write-Host ""
}

# Run tests unless skipped
if (-not $SkipTests) {
    Write-Host "Running tests..." -ForegroundColor Yellow
    dotnet test "$rootDir\HyperV.sln" --configuration $Configuration --no-build --verbosity normal
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Some tests failed. Continuing..."
    } else {
        Write-Host "Tests completed" -ForegroundColor Green
    }
    Write-Host ""
}

# Publish if requested
if ($Publish) {
    $publishDir = Join-Path $rootDir "publish"

    Write-Host "Publishing artifacts..." -ForegroundColor Yellow

    # Publish HyperV.Agent
    Write-Host "  Publishing HyperV.Agent..." -ForegroundColor Cyan
    $agentPublishDir = Join-Path $publishDir "HyperV.Agent"
    dotnet publish "$rootDir\src\HyperV.Agent\HyperV.Agent.csproj" `
        --configuration $Configuration `
        --output $agentPublishDir `
        --no-build `
        --self-contained false
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Agent publish failed"
        exit 1
    }

    # Publish HyperV.CentralManagement
    Write-Host "  Publishing HyperV.CentralManagement..." -ForegroundColor Cyan
    $centralPublishDir = Join-Path $publishDir "HyperV.CentralManagement"
    dotnet publish "$rootDir\src\HyperV.CentralManagement\HyperV.CentralManagement.csproj" `
        --configuration $Configuration `
        --output $centralPublishDir `
        --no-build `
        --self-contained false
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Central Management publish failed"
        exit 1
    }

    Write-Host "Publish completed" -ForegroundColor Green
    Write-Host ""
    Write-Host "Published artifacts location:" -ForegroundColor Cyan
    Write-Host "  HyperV.Agent: $agentPublishDir" -ForegroundColor White
    Write-Host "  HyperV.CentralManagement: $centralPublishDir" -ForegroundColor White
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Build Completed Successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
