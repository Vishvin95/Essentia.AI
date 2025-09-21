# Test script for User Context API - Fixed Version
Write-Host "=== Testing User Context API (Fixed) ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: Process user context
Write-Host "Test 1: Processing user context..." -ForegroundColor Yellow
$body = @{
    UserId = 1
    ContextText = "I will be out of town for a week starting next Monday"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5256/api/usercontext/process-context" -Method POST -Body $body -ContentType "application/json"
    Write-Host "SUCCESS: User context processed!" -ForegroundColor Green
    Write-Host "Context ID: $($response.ContextId)" -ForegroundColor White
    Write-Host "Message: $($response.Message)" -ForegroundColor White
    Write-Host ""
    Write-Host "Saved Context Details:" -ForegroundColor Magenta
    Write-Host "- User ID: $($response.SavedContext.UserId)" -ForegroundColor White
    Write-Host "- Context Text: $($response.SavedContext.ContextText)" -ForegroundColor White
    Write-Host "- Created At: $($response.SavedContext.CreatedAt)" -ForegroundColor White
    Write-Host ""
    Write-Host "Structured Context:" -ForegroundColor Magenta
    $structuredContext = $response.SavedContext.StructuredContext
    Write-Host "- Type: $($structuredContext.Type)" -ForegroundColor White
    Write-Host "- Confidence: $($structuredContext.Confidence)" -ForegroundColor White
    Write-Host "- Tags: $($structuredContext.Tags -join ', ')" -ForegroundColor White
    
    # Store context ID for next test
    $global:contextId = $response.ContextId
    Write-Host ""
    Write-Host "✓ Test 1 PASSED" -ForegroundColor Green
} catch {
    Write-Host "✗ Test 1 FAILED" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Waiting 2 seconds before next test..." -ForegroundColor Gray
Start-Sleep -Seconds 2

# Test 2: Get user contexts
Write-Host "Test 2: Retrieving user contexts..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5256/api/usercontext/user/1?limit=10" -Method GET
    Write-Host "SUCCESS: Retrieved user contexts!" -ForegroundColor Green
    Write-Host "Number of contexts: $($response.Count)" -ForegroundColor White
    
    if ($response.Count -gt 0) {
        Write-Host ""
        Write-Host "Latest Context:" -ForegroundColor Magenta
        $latest = $response[0]
        Write-Host "- Context ID: $($latest.ContextId)" -ForegroundColor White
        Write-Host "- Context Text: $($latest.ContextText)" -ForegroundColor White
        Write-Host "- Type: $($latest.StructuredContext.Type)" -ForegroundColor White
    }
    
    Write-Host ""
    Write-Host "✓ Test 2 PASSED" -ForegroundColor Green
} catch {
    Write-Host "✗ Test 2 FAILED" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== User Context API Test Complete ===" -ForegroundColor Cyan