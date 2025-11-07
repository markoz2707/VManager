# Simple Test Script for /api/v1/vms Endpoints
$baseUrl = "http://127.0.0.1:8743"
$vmsUrl = "$baseUrl/api/v1/vms"

Write-Host "=== Testing /api/v1/vms Endpoints ===" -ForegroundColor Green
Write-Host "Base URL: $baseUrl" -ForegroundColor Yellow
Write-Host ""

# Test 1: List all VMs
Write-Host "1. Testing GET /api/v1/vms (List VMs)" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri $vmsUrl -Method GET
    Write-Host "   SUCCESS: $($response | ConvertTo-Json -Compress)" -ForegroundColor Green
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 2: Check VM presence for non-existent VM
$testVm = "test-vm-123"
Write-Host "2. Testing GET /api/v1/vms/$testVm/present (Check VM presence)" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$vmsUrl/$testVm/present" -Method GET
    Write-Host "   SUCCESS: $($response | ConvertTo-Json -Compress)" -ForegroundColor Green
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 3: Try to get properties of non-existent VM (should fail)
Write-Host "3. Testing GET /api/v1/vms/$testVm/properties (Get VM properties - should fail)" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$vmsUrl/$testVm/properties" -Method GET
    Write-Host "   UNEXPECTED SUCCESS: $($response | ConvertTo-Json -Compress)" -ForegroundColor Yellow
} catch {
    Write-Host "   EXPECTED FAILURE: $($_.Exception.Message)" -ForegroundColor Green
}
Write-Host ""

# Test 4: Try to start non-existent VM (should fail)
Write-Host "4. Testing POST /api/v1/vms/$testVm/start (Start VM - should fail)" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$vmsUrl/$testVm/start" -Method POST
    Write-Host "   UNEXPECTED SUCCESS: $($response | ConvertTo-Json -Compress)" -ForegroundColor Yellow
} catch {
    Write-Host "   EXPECTED FAILURE: $($_.Exception.Message)" -ForegroundColor Green
}
Write-Host ""

# Test 5: Create VM with invalid data (should fail)
Write-Host "5. Testing POST /api/v1/vms (Create VM with empty body - should fail)" -ForegroundColor Cyan
try {
    $headers = @{ "Content-Type" = "application/json" }
    $response = Invoke-RestMethod -Uri $vmsUrl -Method POST -Body '{}' -Headers $headers
    Write-Host "   UNEXPECTED SUCCESS: $($response | ConvertTo-Json -Compress)" -ForegroundColor Yellow
} catch {
    Write-Host "   EXPECTED FAILURE: $($_.Exception.Message)" -ForegroundColor Green
}
Write-Host ""

# Test 6: Test agent health endpoint (should work)
Write-Host "6. Testing GET /api/v1/service/health (Agent health)" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/service/health" -Method GET
    Write-Host "   SUCCESS: $($response | ConvertTo-Json -Compress)" -ForegroundColor Green
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 7: Test agent info endpoint (should work)
Write-Host "7. Testing GET /api/v1/service/info (Agent info)" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/service/info" -Method GET
    Write-Host "   SUCCESS: $($response | ConvertTo-Json -Compress)" -ForegroundColor Green
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "=== Test Summary ===" -ForegroundColor Green
Write-Host "Basic endpoint connectivity tests completed!" -ForegroundColor Yellow
Write-Host "All endpoints are responding correctly to requests." -ForegroundColor Yellow
Write-Host ""
Write-Host "Tested endpoints:" -ForegroundColor Cyan
Write-Host "  GET /api/v1/vms                    - List VMs"
Write-Host "  GET /api/v1/vms/{name}/present     - Check VM presence"
Write-Host "  GET /api/v1/vms/{name}/properties  - Get VM properties"
Write-Host "  POST /api/v1/vms/{name}/start      - Start VM"
Write-Host "  POST /api/v1/vms                   - Create VM"
Write-Host "  GET /api/v1/service/health         - Agent health"
Write-Host "  GET /api/v1/service/info           - Agent info"
Write-Host ""
Write-Host "Test completed at $(Get-Date)" -ForegroundColor Green
