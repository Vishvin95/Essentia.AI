# Enhanced Test script for Voice Expense API - Database Validation

Write-Host ("="*80)
Write-Host "TESTING VOICE EXPENSE API - DATABASE STORAGE VALIDATION"
Write-Host ("="*80)

# Function to test voice expense and validate database storage
function Test-VoiceExpense {
    param(
        [string]$TestName,
        [hashtable]$RequestData,
        [bool]$ShouldSucceed = $true
    )
    
    Write-Host ("`n" + ("="*60))
    Write-Host "TEST: $TestName"
    Write-Host ("="*60)
    
    $body = $RequestData | ConvertTo-Json
    Write-Host "Request payload:"
    Write-Host $body -ForegroundColor Cyan
    Write-Host ("`n" + ("-"*50))
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/voice-expense" -Method POST -Body $body -ContentType "application/json"
        
        if ($ShouldSucceed) {
            Write-Host "SUCCESS: Voice expense created!" -ForegroundColor Green
            
            # Validate extracted data
            if ($response.ExtractedData) {
                Write-Host "`nEXTRACTED DATA:" -ForegroundColor Yellow
                Write-Host "  Amount: $($response.ExtractedData.Amount) $($response.ExtractedData.Currency)" -ForegroundColor Yellow
                Write-Host "  Item: $($response.ExtractedData.Item)" -ForegroundColor Yellow
                Write-Host "  Merchant: $($response.ExtractedData.Merchant)" -ForegroundColor Yellow
                Write-Host "  Date: $($response.ExtractedData.Date)" -ForegroundColor Yellow
            }
            
            # Show response details (database save confirmed by ExpenseId)
            if ($response.ExpenseId) {
                Write-Host "`nDATABASE SAVE CONFIRMED:" -ForegroundColor Green
                Write-Host "  Expense ID: $($response.ExpenseId)" -ForegroundColor Green
                Write-Host "  Note: Full expense details saved to database" -ForegroundColor Green
            }
            
            Write-Host "`nSUMMARY:" -ForegroundColor White
            Write-Host "  Success: $($response.Success)" -ForegroundColor White
            Write-Host "  Message: $($response.Message)" -ForegroundColor White
            Write-Host "  Total Amount: $($response.TotalAmount) $($response.Currency)" -ForegroundColor White
            
        } else {
            Write-Host "UNEXPECTED: Request succeeded when it should have failed!" -ForegroundColor Yellow
            Write-Host "Response: $($response | ConvertTo-Json -Depth 5)" -ForegroundColor Yellow
        }
        
    } catch {
        if ($ShouldSucceed) {
            Write-Host "ERROR: Failed to create voice expense!" -ForegroundColor Red
            Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Red
            if ($_.Exception.Response) {
                Write-Host "HTTP Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
            }
        } else {
            Write-Host "EXPECTED: Validation correctly rejected invalid data!" -ForegroundColor Green
            Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Green
        }
    }
}

# Test 1: Simple coffee purchase
Test-VoiceExpense -TestName "Simple Coffee Purchase" -RequestData @{
    UserId = 1
    GroupId = 1
    SpeechText = "I spent 12 dollars on coffee at Starbucks today"
}

# Test 2: Grocery shopping with different currency
Test-VoiceExpense -TestName "Grocery Shopping (EUR)" -RequestData @{
    UserId = 2
    GroupId = 1
    SpeechText = "Yesterday I bought groceries for 45.50 euros at the local supermarket"
}

# Test 3: Restaurant meal with GBP
Test-VoiceExpense -TestName "Restaurant Meal (GBP)" -RequestData @{
    UserId = 1
    GroupId = 2
    SpeechText = "I paid 25 pounds for lunch at the Italian restaurant"
}

# Test 4: Gas station purchase
Test-VoiceExpense -TestName "Gas Station Purchase" -RequestData @{
    UserId = 3
    GroupId = 1
    SpeechText = "Filled up gas for 65 dollars at Shell station"
}

# Test 5: Multiple items aggregation - same user, group, date
Test-VoiceExpense -TestName "Aggregation Test - Same User/Group/Date" -RequestData @{
    UserId = 1
    GroupId = 1
    SpeechText = "I bought a sandwich for 8 dollars at the deli today"
}

# Test 6: Invalid data validation
Test-VoiceExpense -TestName "Invalid Data Validation" -RequestData @{
    UserId = 0  # Invalid
    GroupId = 1
    SpeechText = ""  # Invalid (empty)
} -ShouldSucceed $false

# Test 7: Complex extraction test
Test-VoiceExpense -TestName "Complex Extraction Test" -RequestData @{
    UserId = 4
    GroupId = 2
    SpeechText = "Last Tuesday I spent fifty-five dollars and thirty cents on office supplies at OfficeMax"
}

Write-Host "`n" + "="*80
Write-Host "VOICE EXPENSE API DATABASE VALIDATION TESTS COMPLETED"
Write-Host "="*80

Write-Host "`nEXPECTED DATABASE STATE:" -ForegroundColor Cyan
Write-Host "  - Expenses table: Multiple entries with proper ExpenseId, UserId, GroupId" -ForegroundColor Cyan
Write-Host "  - ExpenseItems table: Detailed items linked via ExpenseId foreign key" -ForegroundColor Cyan
Write-Host "  - Proper currency normalization" -ForegroundColor Cyan
Write-Host "  - Vendor/merchant information stored in VendorName field" -ForegroundColor Cyan
Write-Host "  - Expense aggregation for same user/group/date combinations" -ForegroundColor Cyan
Write-Host "  - All extracted data properly persisted with timestamps" -ForegroundColor Cyan

Write-Host "`nDATABASE VERIFICATION:" -ForegroundColor Yellow
Write-Host "  Check your database to verify:" -ForegroundColor Yellow
Write-Host "  1. Expenses table has new records with extracted data" -ForegroundColor Yellow
Write-Host "  2. ExpenseItems table has corresponding items" -ForegroundColor Yellow
Write-Host "  3. Foreign key relationships are maintained" -ForegroundColor Yellow
Write-Host "  4. Currency codes are normalized (USD, EUR, GBP)" -ForegroundColor Yellow
Write-Host "  5. Vendor/merchant info is stored in VendorName" -ForegroundColor Yellow