# Test script for Voice Expense API

Write-Host "="*60
Write-Host "TESTING VOICE EXPENSE API"
Write-Host "="*60

# Test 1: Create a voice expense with simple text
Write-Host "Test 1: Creating voice expense with simple text..."

$voiceExpense1 = @{
    UserId = 1
    GroupId = 1
    SpeechText = "I spent 12 dollars on coffee at Starbucks today"
} | ConvertTo-Json

Write-Host "Request payload:"
Write-Host $voiceExpense1
Write-Host "`n" + "-"*50

try {
    $response1 = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/voice-expense" -Method POST -Body $voiceExpense1 -ContentType "application/json"
    Write-Host "✅ SUCCESS: Voice expense created!" -ForegroundColor Green
    Write-Host "Response: $($response1 | ConvertTo-Json -Depth 4)" -ForegroundColor Green
} catch {
    Write-Host "❌ ERROR: Failed to create voice expense!" -ForegroundColor Red
    Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "HTTP Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
}

Write-Host "`n" + "="*50 + "`n"

# Test 2: Create voice expense with more complex text
Write-Host "Test 2: Creating voice expense with complex text..."

$voiceExpense2 = @{
    UserId = 2
    GroupId = 1
    SpeechText = "Yesterday I bought groceries for 45.50 euros at the local supermarket"
} | ConvertTo-Json

Write-Host "Request payload:"
Write-Host $voiceExpense2
Write-Host "`n" + "-"*50

try {
    $response2 = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/voice-expense" -Method POST -Body $voiceExpense2 -ContentType "application/json"
    Write-Host "✅ SUCCESS: Complex voice expense created!" -ForegroundColor Green
    Write-Host "Response: $($response2 | ConvertTo-Json -Depth 4)" -ForegroundColor Green
} catch {
    Write-Host "❌ ERROR: Failed to create complex voice expense!" -ForegroundColor Red
    Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n" + "="*50 + "`n"

# Test 3: Create voice expense without specific date (should default to today)
Write-Host "Test 3: Creating voice expense without specific date..."

$voiceExpense3 = @{
    UserId = 1
    GroupId = 2
    SpeechText = "I paid 25 pounds for lunch at the restaurant"
} | ConvertTo-Json

Write-Host "Request payload:"
Write-Host $voiceExpense3
Write-Host "`n" + "-"*50

try {
    $response3 = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/voice-expense" -Method POST -Body $voiceExpense3 -ContentType "application/json"
    Write-Host "✅ SUCCESS: Voice expense with default date created!" -ForegroundColor Green
    Write-Host "Response: $($response3 | ConvertTo-Json -Depth 4)" -ForegroundColor Green
} catch {
    Write-Host "❌ ERROR: Failed to create voice expense with default date!" -ForegroundColor Red
    Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n" + "="*50 + "`n"

# Test 4: Test validation with invalid data
Write-Host "Test 4: Testing validation with invalid data..."

$invalidVoiceExpense = @{
    UserId = 0  # Invalid
    GroupId = 1
    SpeechText = ""  # Invalid (empty)
} | ConvertTo-Json

Write-Host "Request payload (should fail validation):"
Write-Host $invalidVoiceExpense
Write-Host "`n" + "-"*50

try {
    $response4 = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/voice-expense" -Method POST -Body $invalidVoiceExpense -ContentType "application/json"
    Write-Host "❌ UNEXPECTED: Invalid data was accepted!" -ForegroundColor Yellow
    Write-Host "Response: $($response4 | ConvertTo-Json -Depth 4)" -ForegroundColor Yellow
} catch {
    Write-Host "✅ SUCCESS: Validation correctly rejected invalid data!" -ForegroundColor Green
    Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Green
    if ($_.Exception.Response) {
        Write-Host "HTTP Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Green
    }
}

Write-Host "`n" + "="*60
Write-Host "VOICE EXPENSE API TESTS COMPLETED"
Write-Host "="*60

Write-Host "`n📊 Summary of expected behavior:"
Write-Host "  • Simple expense: $12 USD for coffee at Starbucks"
Write-Host "  • Complex expense: €45.50 EUR for groceries at supermarket (yesterday)"
Write-Host "  • Default date expense: £25 GBP for lunch at restaurant (today)"
Write-Host "  • Invalid data: Properly rejected"
Write-Host "`n🤖 Note: This uses mock extraction service. Configure Azure OpenAI for production use."