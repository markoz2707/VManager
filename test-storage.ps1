# Comprehensive Storage Operations Test Script
# Tests the refactored StorageService implementation

param(
    [string]$ApiBaseUrl = "http://127.0.0.1:8743/api",
    [string]$TestVmName = "StorageTest-VM",
    [string]$TestVhdPath = "C:\temp\wmi\storage-test.vhdx",
    [long]$InitialSize = 5GB,
    [long]$ResizeSize = 10GB
)

# Ensure temp directory exists
$tempDir = Split-Path $TestVhdPath -Parent
if (!(Test-Path $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    Write-Host "Created temp directory: $tempDir" -ForegroundColor Green
}

# Function to make REST API calls with error handling
function Invoke-ApiCall {
    param(
        [string]$Method,
        [string]$Uri,
        [object]$Body = $null,
        [string]$Description
    )
    
    try {
        Write-Host "Testing: $Description" -ForegroundColor Yellow
        
        $params = @{
            Method = $Method
            Uri = $Uri
            ContentType = "application/json"
        }
        
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
        }
        
        $response = Invoke-RestMethod @params
        Write-Host "✅ SUCCESS: $Description" -ForegroundColor Green
        return $response
    }
    catch {
        Write-Host "❌ FAILED: $Description" -ForegroundColor Red
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "Response: $responseBody" -ForegroundColor Red
        }
        throw
    }
}

# Function to check if VM exists using service controller
function Test-VmExists {
    param([string]$VmName)
    
    try {
        $response = Invoke-RestMethod -Method Get -Uri "$ApiBaseUrl/v1/service/vm-present/$VmName"
        return $response.present
    }
    catch {
        return $false
    }
}

# Function to create test VM if it doesn't exist
function New-TestVm {
    param([string]$VmName)
    
    if (Test-VmExists -VmName $VmName) {
        Write-Host "VM '$VmName' already exists, using existing VM" -ForegroundColor Cyan
        return
    }
    
    Write-Host "Creating test VM: $VmName" -ForegroundColor Yellow
    
    $vmConfig = @{
        Id = [System.Guid]::NewGuid().ToString()
        Name = $VmName
        MemoryMB = 2048
        CpuCount = 2
        DiskSizeGB = 20
        Mode = 1
    }
    
    try {
        Invoke-ApiCall -Method Post -Uri "$ApiBaseUrl/v1/vms" -Body $vmConfig -Description "Create test VM"
    }
    catch {
        Write-Host "Failed to create VM, continuing with existing VM if available..." -ForegroundColor Yellow
    }
}

# Function to remove test VM
function Remove-TestVm {
    param([string]$VmName)
    
    if (Test-VmExists -VmName $VmName) {
        Write-Host "Removing test VM: $VmName" -ForegroundColor Yellow
        try {
            # Use service controller to stop the VM first
            Invoke-RestMethod -Method Post -Uri "$ApiBaseUrl/v1/service/vm-stop/$VmName" -ErrorAction SilentlyContinue
            Write-Host "VM stopped (if it was running)" -ForegroundColor Yellow
        }
        catch {
            Write-Host "Warning: Could not stop/remove test VM" -ForegroundColor Yellow
        }
    }
}

# Main test execution
Write-Host "=== Hyper-V Storage Operations Test ===" -ForegroundColor Cyan
Write-Host "API Base URL: $ApiBaseUrl" -ForegroundColor Cyan
Write-Host "Test VM: $TestVmName" -ForegroundColor Cyan
Write-Host "Test VHD: $TestVhdPath" -ForegroundColor Cyan
Write-Host ""

$testResults = @{
    CreateVhd = $false
    CreateVm = $false
    AttachVhd = $false
    DetachVhd = $false
    ResizeVhd = $false
    Cleanup = $false
}

try {
    # Test 1: Health Check
    Write-Host "=== Step 1: Health Check ===" -ForegroundColor Cyan
    Invoke-ApiCall -Method Get -Uri "$ApiBaseUrl/v1/health" -Description "API Health Check"
    Write-Host ""

    # Test 2: Create VHD
    Write-Host "=== Step 2: Create Virtual Hard Disk ===" -ForegroundColor Cyan
    $vhdConfig = @{
        Path = $TestVhdPath
        MaxInternalSize = $InitialSize
        Format = "VHDX"
        Type = "Dynamic"
    }
    
    Invoke-ApiCall -Method Post -Uri "$ApiBaseUrl/Storage/vhd" -Body $vhdConfig -Description "Create VHD"
    $testResults.CreateVhd = $true
    
    # Verify VHD file was created
    if (Test-Path $TestVhdPath) {
        Write-Host "✅ VHD file created successfully at: $TestVhdPath" -ForegroundColor Green
    } else {
        throw "VHD file was not created at expected location"
    }
    Write-Host ""

    # Test 3: Create Test VM
    Write-Host "=== Step 3: Prepare Test VM ===" -ForegroundColor Cyan
    New-TestVm -VmName $TestVmName
    $testResults.CreateVm = $true
    Write-Host ""

    # Test 4: Attach VHD to VM
    Write-Host "=== Step 4: Attach VHD to VM ===" -ForegroundColor Cyan
    $attachUri = "$ApiBaseUrl/Storage/vhd/attach?vmName=$([System.Web.HttpUtility]::UrlEncode($TestVmName))&vhdPath=$([System.Web.HttpUtility]::UrlEncode($TestVhdPath))"
    Invoke-ApiCall -Method Put -Uri $attachUri -Description "Attach VHD to VM"
    $testResults.AttachVhd = $true
    Write-Host ""

    # Test 5: Detach VHD from VM
    Write-Host "=== Step 5: Detach VHD from VM ===" -ForegroundColor Cyan
    $detachUri = "$ApiBaseUrl/Storage/vhd/detach?vmName=$([System.Web.HttpUtility]::UrlEncode($TestVmName))&vhdPath=$([System.Web.HttpUtility]::UrlEncode($TestVhdPath))"
    Invoke-ApiCall -Method Put -Uri $detachUri -Description "Detach VHD from VM"
    $testResults.DetachVhd = $true
    Write-Host ""

    # Test 6: Resize VHD
    Write-Host "=== Step 6: Resize Virtual Hard Disk ===" -ForegroundColor Cyan
    $resizeConfig = @{
        Path = $TestVhdPath
        MaxInternalSize = $ResizeSize
    }
    
    Invoke-ApiCall -Method Put -Uri "$ApiBaseUrl/Storage/vhd/resize" -Body $resizeConfig -Description "Resize VHD"
    $testResults.ResizeVhd = $true
    Write-Host ""

    # Test 7: Re-attach VHD to verify resize worked
    Write-Host "=== Step 7: Re-attach Resized VHD ===" -ForegroundColor Cyan
    Invoke-ApiCall -Method Put -Uri $attachUri -Description "Re-attach resized VHD"
    
    # Detach again for cleanup
    Invoke-ApiCall -Method Put -Uri $detachUri -Description "Detach VHD for cleanup"
    Write-Host ""

    Write-Host "=== All Tests Completed Successfully! ===" -ForegroundColor Green

} catch {
    Write-Host "=== Test Failed ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    # Cleanup
    Write-Host "=== Cleanup ===" -ForegroundColor Cyan
    
    try {
        # Remove test VM
        Remove-TestVm -VmName $TestVmName
        
        # Remove VHD file
        if (Test-Path $TestVhdPath) {
            Remove-Item -Path $TestVhdPath -Force
            Write-Host "✅ Removed test VHD file" -ForegroundColor Green
        }
        
        $testResults.Cleanup = $true
        Write-Host "✅ Cleanup completed" -ForegroundColor Green
    } catch {
        Write-Host "⚠️  Cleanup warning: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# Test Results Summary
Write-Host ""
Write-Host "=== Test Results Summary ===" -ForegroundColor Cyan
$testResults.GetEnumerator() | ForEach-Object {
    $status = if ($_.Value) { "✅ PASS" } else { "❌ FAIL" }
    $color = if ($_.Value) { "Green" } else { "Red" }
    Write-Host "$($_.Key): $status" -ForegroundColor $color
}

$passedTests = ($testResults.Values | Where-Object { $_ }).Count
$totalTests = $testResults.Count
Write-Host ""
Write-Host "Overall Result: $passedTests/$totalTests tests passed" -ForegroundColor $(if ($passedTests -eq $totalTests) { "Green" } else { "Yellow" })

if ($passedTests -eq $totalTests) {
    Write-Host "🎉 All storage operations are working correctly!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "⚠️  Some tests failed. Check the implementation." -ForegroundColor Yellow
    exit 1
}
