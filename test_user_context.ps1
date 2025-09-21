# Test script for User Context Processing API
Write-Host "`n=== Testing User Context Processing API ===" -ForegroundColor Cyan

# Base URL - adjust if needed
$baseUrl = "http://localhost:5256/api/usercontext"

Write-Host "`n1. Testing Process Context API" -ForegroundColor Yellow
Write-Host "Endpoint: POST $baseUrl/process-context" -ForegroundColor Gray

# Test Case 1: Travel context
Write-Host "`nTest Case 1: Travel Context" -ForegroundColor White
$travelContext = @{
    UserId = 1
    ContextText = "I will be out of town for a week starting next Monday for a business trip to New York."
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/process-context" -Method POST -Body $travelContext -ContentType "application/json"
    
    Write-Host "SUCCESS: Travel context processed!" -ForegroundColor Green
    Write-Host "Context ID: $($response.ContextId)" -ForegroundColor Yellow
    Write-Host "Message: $($response.Message)" -ForegroundColor Cyan
    Write-Host "Extracted Type: $($response.SavedContext.StructuredContext.Type)" -ForegroundColor Magenta
    Write-Host "Confidence: $($response.SavedContext.StructuredContext.Confidence)" -ForegroundColor Magenta
    Write-Host "Tags: $($response.SavedContext.StructuredContext.Tags -join ', ')" -ForegroundColor Magenta
    
    if ($response.SavedContext.StructuredContext.DateReferences.Count -gt 0) {
        Write-Host "Date References:" -ForegroundColor DarkYellow
        foreach ($dateRef in $response.SavedContext.StructuredContext.DateReferences) {
            Write-Host "  - $($dateRef.OriginalText) -> $($dateRef.ParsedDate) (Confidence: $($dateRef.Confidence))" -ForegroundColor DarkGray
        }
    }
    
    # Store the context ID for later tests
    $script:testContextId1 = $response.ContextId
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
}

# Test Case 2: Event context
Write-Host "`nTest Case 2: Event Context" -ForegroundColor White
$eventContext = @{
    UserId = 1
    ContextText = "I am hosting a party on September 21st with John and Sarah."
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/process-context" -Method POST -Body $eventContext -ContentType "application/json"
    
    Write-Host "SUCCESS: Event context processed!" -ForegroundColor Green
    Write-Host "Context ID: $($response.ContextId)" -ForegroundColor Yellow
    Write-Host "Extracted Type: $($response.SavedContext.StructuredContext.Type)" -ForegroundColor Magenta
    Write-Host "People References: $($response.SavedContext.StructuredContext.PeopleReferences -join ', ')" -ForegroundColor Magenta
    
    # Store the context ID for later tests
    $script:testContextId2 = $response.ContextId
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

# Test Case 3: Personal context
Write-Host "`nTest Case 3: Personal Context" -ForegroundColor White
$personalContext = @{
    UserId = 1
    ContextText = "I have a doctor's appointment tomorrow at 3 PM and need to take medication twice daily."
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/process-context" -Method POST -Body $personalContext -ContentType "application/json"
    
    Write-Host "SUCCESS: Personal context processed!" -ForegroundColor Green
    Write-Host "Context ID: $($response.ContextId)" -ForegroundColor Yellow
    Write-Host "Extracted Type: $($response.SavedContext.StructuredContext.Type)" -ForegroundColor Magenta
    
    # Store the context ID for later tests
    $script:testContextId3 = $response.ContextId
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n2. Testing Get User Contexts API" -ForegroundColor Yellow
Write-Host "Endpoint: GET $baseUrl/user/{userId}/contexts" -ForegroundColor Gray

try {
    Write-Host "`nGetting all contexts for User 1..." -ForegroundColor White
    $response = Invoke-RestMethod -Uri "$baseUrl/user/1/contexts" -Method GET
    
    Write-Host "SUCCESS: Retrieved $($response.Count) contexts!" -ForegroundColor Green
    foreach ($context in $response) {
        Write-Host "  - Context ID: $($context.ContextId)" -ForegroundColor Cyan
        Write-Host "    Type: $($context.StructuredContext.Type)" -ForegroundColor White
        Write-Host "    Text: $($context.ContextText.Substring(0, [Math]::Min(50, $context.ContextText.Length)))..." -ForegroundColor Gray
        Write-Host "    Created: $($context.CreatedAt)" -ForegroundColor Gray
        Write-Host ""
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n3. Testing Get Specific Context API" -ForegroundColor Yellow
Write-Host "Endpoint: GET $baseUrl/context/{contextId}/user/{userId}" -ForegroundColor Gray

if ($script:testContextId1) {
    try {
        Write-Host "`nGetting specific context: $($script:testContextId1)" -ForegroundColor White
        $response = Invoke-RestMethod -Uri "$baseUrl/context/$($script:testContextId1)/user/1" -Method GET
        
        Write-Host "SUCCESS: Retrieved specific context!" -ForegroundColor Green
        Write-Host "Context ID: $($response.ContextId)" -ForegroundColor Cyan
        Write-Host "Context Text: $($response.ContextText)" -ForegroundColor White
        Write-Host "Type: $($response.StructuredContext.Type)" -ForegroundColor Magenta
        
        Write-Host "`nExtracted Data:" -ForegroundColor DarkYellow
        foreach ($key in $response.StructuredContext.ExtractedData.Keys) {
            Write-Host "  - $key: $($response.StructuredContext.ExtractedData[$key])" -ForegroundColor DarkGray
        }
    } catch {
        Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n4. Testing Search Contexts API" -ForegroundColor Yellow
Write-Host "Endpoint: GET $baseUrl/user/{userId}/search" -ForegroundColor Gray

try {
    Write-Host "`nSearching for 'travel' type contexts..." -ForegroundColor White
    $response = Invoke-RestMethod -Uri "$baseUrl/user/1/search?type=travel" -Method GET
    
    Write-Host "SUCCESS: Found $($response.Count) travel contexts!" -ForegroundColor Green
    foreach ($context in $response) {
        Write-Host "  - $($context.ContextId): $($context.ContextText.Substring(0, [Math]::Min(60, $context.ContextText.Length)))..." -ForegroundColor White
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

try {
    Write-Host "`nSearching for contexts with 'party' tag..." -ForegroundColor White
    $response = Invoke-RestMethod -Uri "$baseUrl/user/1/search?tag=party" -Method GET
    
    Write-Host "SUCCESS: Found $($response.Count) contexts with 'party' tag!" -ForegroundColor Green
    foreach ($context in $response) {
        Write-Host "  - $($context.ContextId): $($context.ContextText.Substring(0, [Math]::Min(60, $context.ContextText.Length)))..." -ForegroundColor White
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n5. Testing Error Scenarios" -ForegroundColor Yellow

# Test with empty context text
try {
    Write-Host "`nTesting with empty context text..." -ForegroundColor White
    $emptyContext = @{
        UserId = 1
        ContextText = ""
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$baseUrl/process-context" -Method POST -Body $emptyContext -ContentType "application/json"
    Write-Host "Unexpected success with empty context" -ForegroundColor Red
} catch {
    Write-Host "Expected error for empty context: $($_.Exception.Message)" -ForegroundColor Green
}

# Test with invalid user ID
try {
    Write-Host "`nTesting with invalid user ID..." -ForegroundColor White
    $invalidUser = @{
        UserId = 0
        ContextText = "Test context"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$baseUrl/process-context" -Method POST -Body $invalidUser -ContentType "application/json"
    Write-Host "Unexpected success with invalid user ID" -ForegroundColor Red
} catch {
    Write-Host "Expected error for invalid user ID: $($_.Exception.Message)" -ForegroundColor Green
}

# Test getting non-existent context
try {
    Write-Host "`nTesting with non-existent context ID..." -ForegroundColor White
    $response = Invoke-RestMethod -Uri "$baseUrl/context/INVALID_CTX/user/1" -Method GET
    Write-Host "Unexpected success with non-existent context" -ForegroundColor Red
} catch {
    Write-Host "Expected error for non-existent context: $($_.Exception.Message)" -ForegroundColor Green
}

Write-Host "`n6. Testing Delete Context API" -ForegroundColor Yellow
Write-Host "Endpoint: DELETE $baseUrl/context/{contextId}/user/{userId}" -ForegroundColor Gray

if ($script:testContextId3) {
    try {
        Write-Host "`nDeleting context: $($script:testContextId3)" -ForegroundColor White
        $response = Invoke-RestMethod -Uri "$baseUrl/context/$($script:testContextId3)/user/1" -Method DELETE
        
        Write-Host "SUCCESS: Context deleted!" -ForegroundColor Green
        Write-Host "Message: $($response.Message)" -ForegroundColor Cyan
        
        # Verify it's gone
        try {
            $response = Invoke-RestMethod -Uri "$baseUrl/context/$($script:testContextId3)/user/1" -Method GET
            Write-Host "WARNING: Context still accessible after deletion" -ForegroundColor Yellow
        } catch {
            Write-Host "Confirmed: Context no longer accessible" -ForegroundColor Green
        }
    } catch {
        Write-Host "ERROR deleting context: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n=== User Context API Testing Complete ===" -ForegroundColor Cyan
Write-Host "Note: Make sure Cosmos DB is properly configured and accessible for full functionality." -ForegroundColor Yellow