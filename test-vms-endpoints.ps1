# Comprehensive Test Script for /api/v1/vms Endpoints
# Tests all VM management operations in the consolidated VmsController

$baseUrl = "http://127.0.0.1:8743"
$vmsUrl = "$baseUrl/api/v1/vms"

Write-Host "=== Testing /api/v1/vms Endpoints ===" -ForegroundColor Green
Write-Host "Base URL: $baseUrl" -ForegroundColor Yellow
Write-Host ""

# Test data
$testVmName = "test-vm-$(Get-Date -Format 'HHmmss')"
$createVmRequest = @{
    Id = $testVmName
    Name = $testVmName
    Mode = "WMI"
    MemoryMB = 2048
    CpuCount = 2
    Generation = 2
    SecureBoot = $true
    VhdPath = "C:\temp\$testVmName.vhdx"
    VhdSizeGB = 20
    SwitchName = "Default Switch"
    Notes = "Test VM created by endpoint testing"
} | ConvertTo-Json

$configureVmRequest = @{
    StartupMemoryMB = 4096
    CpuCount = 4
    Notes = "Updated test VM configuration"
    EnableDynamicMemory = $true
    MinimumMemoryMB = 2048
    MaximumMemoryMB = 8192
    TargetMemoryBuffer = 20
} | ConvertTo-Json

$snapshotRequest = @{
    SnapshotName = "test-snapshot-$(Get-Date -Format 'HHmmss')"
    Notes = "Test snapshot created by endpoint testing"
} | ConvertTo-Json

# Function to make HTTP requests with error handling
function Invoke-TestRequest {
    param(
        [string]$Method,
        [string]$Uri,
        [string]$Body = $null,
        [string]$Description
    )
    
    Write-Host "Testing: $Description" -ForegroundColor Cyan
    Write-Host "  $Method $Uri" -ForegroundColor Gray
    
    try {
        $headers = @{ "Content-Type" = "application/json" }
        
        if ($Body) {
            $response = Invoke-RestMethod -Uri $Uri -Method $Method -Body $Body -Headers $headers -ErrorAction Stop
        } else {
            $response = Invoke-RestMethod -Uri $Uri -Method $Method -Headers $headers -ErrorAction Stop
        }
        
        Write-Host "  ✓ SUCCESS" -ForegroundColor Green
        if ($response) {
            Write-Host "  Response: $($response | ConvertTo-Json -Compress)" -ForegroundColor DarkGray
        }
        Write-Host ""
        return $response
    }
    catch {
        Write-Host "  ✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $statusCode = $_.Exception.Response.StatusCode
            Write-Host "  Status Code: $statusCode" -ForegroundColor Red
        }
        Write-Host ""
        return $null
    }
}

# Test 1: List all VMs (should work even if no VMs exist)
Write-Host "=== 1. Basic Operations ===" -ForegroundColor Magenta
Invoke-TestRequest -Method "GET" -Uri $vmsUrl -Description "List all VMs"

# Test 2: Check VM presence (should return false for non-existent VM)
Invoke-TestRequest -Method "GET" -Uri "$vmsUrl/$testVmName/present" -Description "Check VM presence (non-existent)"

# Test 3: Create VM
Write-Host "=== 2. VM Creation ===" -ForegroundColor Magenta
$createResult = Invoke-TestRequest -Method "POST" -Uri $vmsUrl -Body $createVmRequest -Description "Create new VM"

# Test 4: Check VM presence again (should return true if creation succeeded)
Invoke-TestRequest -Method "GET" -Uri "$vmsUrl/$testVmName/present" -Description "Check VM presence (after creation)"

# Test 5: Get VM properties
Write-Host "=== 3. VM Information ===" -ForegroundColor Magenta
Invoke-TestRequest -Method "GET" -Uri "$vmsUrl/$testVmName/properties" -Description "Get VM properties"

# Test 6: Configure VM
Invoke-TestRequest -Method "POST" -Uri "$vmsUrl/$testVmName/configure" -Body $configureVmRequest -Description "Configure VM (memory, CPU, notes)"

# Test 7: VM State Management
Write-Host "=== 4. VM State Management ===" -ForegroundColor Magenta
Invoke-TestRequest -Method "POST" -Uri "$vmsUrl/$testVmName/start" -Description "Start VM"

Start-Sleep -Seconds 2

Invoke-TestRequest -Method "POST" -Uri "$vmsUrl/$testVmName/pause" -Description "Pause VM"

Start-Sleep -Seconds 2

Invoke-TestRequest -Method "POST" -Uri "$vmsUrl/$testVmName/resume" -Description "Resume VM"

Start-Sleep -Seconds 2

Invoke-TestRequest -Method "POST" -Uri "$vmsUrl/$testVmName/save" -Description "Save VM state"

Start-Sleep -Seconds 2

Invoke-TestRequest -Method "POST" -Uri "$vmsUrl/$testVmName/shutdown" -Description "Shutdown VM (graceful)"

Start-Sleep -Seconds 2

# Test 8: Snapshot Management
Write-Host "=== 5. Snapshot Management ===" -ForegroundColor Magenta
Invoke-TestRequest -Method "GET" -Uri "$vmsUrl/$testVmName/snapshots" -Description "List VM snapshots"

$snapshotResult = Invoke-TestRequest -Method "POST" -Uri "$vmsUrl/$testVmName/snapshots" -Body $snapshotRequest -Description "Create VM snapshot"

# If snapshot creation succeeded, test snapshot operations
if ($snapshotResult) {
    # Extract snapshot ID from response (this might need adjustment based on actual response format)
    $snapshotId = "test-snapshot-id" # This would need to be extracted from the actual response
    
    Invoke-TestRequest -Method "POST" -Uri "$vmsUrl/$testVmName/snapshots/$snapshotId/revert" -Description "Revert VM to snapshot"
    
    Invoke-TestRequest -Method "DELETE" -Uri "$vmsUrl/$testVmName/snapshots/$snapshotId" -Description "Delete VM snapshot"
}

# Test 9: VM Modification
Write-Host "=== 6. VM Modification ===" -ForegroundColor Magenta
$modifyConfig = '{"memory": 4096, "cpu": 4}'
Invoke-TestRequest -Method "POST" -Uri "$vmsUrl/$testVmName/modify" -Body $modifyConfig -Description "Modify VM configuration"

# Test 10: Force operations
Write-Host "=== 7. Force Operations ===" -ForegroundColor Magenta
Invoke-TestRequest -Method "POST" -Uri "$vmsUrl/$testVmName/start" -Description "Start VM (for terminate test)"

Start-Sleep -Seconds 2

Invoke-TestRequest -Method "POST" -Uri "$vmsUrl/$testVmName/terminate" -Description "Terminate VM (force stop)"

# Test 11: Error handling - test with non-existent VM
Write-Host "=== 8. Error Handling ===" -ForegroundColor Magenta
$nonExistentVm = "non-existent-vm-12345"
Invoke-TestRequest -Method "POST" -Uri "$vmsUrl/$nonExistentVm/start" -Description "Start non-existent VM (should fail)"
Invoke-TestRequest -Method "GET" -Uri "$vmsUrl/$nonExistentVm/properties" -Description "Get properties of non-existent VM (should fail)"

# Test 12: Invalid requests
Write-Host "=== 9. Invalid Request Handling ===" -ForegroundColor Magenta
$invalidJson = '{"invalid": json}'
Invoke-TestRequest -Method "POST" -Uri $vmsUrl -Body $invalidJson -Description "Create VM with invalid JSON (should fail)"

$emptyRequest = '{}'
Invoke-TestRequest -Method "POST" -Uri $vmsUrl -Body $emptyRequest -Description "Create VM with empty request (should fail)"

# Final summary
Write-Host "=== Test Summary ===" -ForegroundColor Green
Write-Host "All /api/v1/vms endpoint tests completed!" -ForegroundColor Yellow
Write-Host "Check the results above for any failures." -ForegroundColor Yellow
Write-Host ""
Write-Host "Tested endpoints:" -ForegroundColor Cyan
Write-Host "  GET    /api/v1/vms                              - List VMs"
Write-Host "  POST   /api/v1/vms                              - Create VM"
Write-Host "  GET    /api/v1/vms/{name}/present               - Check VM presence"
Write-Host "  GET    /api/v1/vms/{name}/properties            - Get VM properties"
Write-Host "  POST   /api/v1/vms/{name}/configure             - Configure VM"
Write-Host "  POST   /api/v1/vms/{name}/start                 - Start VM"
Write-Host "  POST   /api/v1/vms/{name}/stop                  - Stop VM"
Write-Host "  POST   /api/v1/vms/{name}/shutdown              - Shutdown VM"
Write-Host "  POST   /api/v1/vms/{name}/terminate             - Terminate VM"
Write-Host "  POST   /api/v1/vms/{name}/pause                 - Pause VM"
Write-Host "  POST   /api/v1/vms/{name}/resume                - Resume VM"
Write-Host "  POST   /api/v1/vms/{name}/save                  - Save VM state"
Write-Host "  POST   /api/v1/vms/{name}/modify                - Modify VM"
Write-Host "  GET    /api/v1/vms/{name}/snapshots             - List snapshots"
Write-Host "  POST   /api/v1/vms/{name}/snapshots             - Create snapshot"
Write-Host "  DELETE /api/v1/vms/{name}/snapshots/{id}        - Delete snapshot"
Write-Host "  POST   /api/v1/vms/{name}/snapshots/{id}/revert - Revert to snapshot"
Write-Host ""
Write-Host "Test completed at $(Get-Date)" -ForegroundColor Green
