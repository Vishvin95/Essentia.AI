# Test script for Total Amount and Item Extraction Fixes

Write-Host "Testing Voice Expense API - Total Amount and Item Extraction Fixes" -ForegroundColor Cyan
Write-Host "=" * 65

# Function to test the fixes
function Test-AmountAndItemExtraction {
    param(
        [string]$TestName,
        [string]$SpeechText,
        [double]$ExpectedAmount,
        [string]$ExpectedItem
    )
    
    Write-Host "`n$TestName" -ForegroundColor Yellow
    Write-Host "-" * $TestName.Length -ForegroundColor Yellow
    Write-Host "Speech: `"$SpeechText`"" -ForegroundColor White
    Write-Host "Expected Amount: $ExpectedAmount" -ForegroundColor Green
    Write-Host "Expected Item: $ExpectedItem" -ForegroundColor Green
    
    $body = @{
        UserId = 1
        GroupId = 1
        SpeechText = $SpeechText
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/voice-expense" -Method POST -Body $body -ContentType "application/json"
        
        Write-Host "`n✅ SUCCESS: Expense created with ExpenseId: $($response.ExpenseId)" -ForegroundColor Green
        
        # Test Total Amount Fix
        $extractedAmount = $response.ExtractedData.Amount
        $responseTotal = $response.TotalAmount
        
        Write-Host "`n💰 AMOUNT VERIFICATION:" -ForegroundColor Magenta
        Write-Host "   Extracted Amount: $extractedAmount" -ForegroundColor White
        Write-Host "   Response Total Amount: $responseTotal" -ForegroundColor White
        
        if ($extractedAmount -eq $responseTotal) {
            Write-Host "   ✅ CORRECT: Total amount matches extracted amount!" -ForegroundColor Green
        } else {
            Write-Host "   ❌ INCORRECT: Total amount ($responseTotal) differs from extracted amount ($extractedAmount)" -ForegroundColor Red
        }
        
        # Test Item Extraction Fix
        $extractedItem = $response.ExtractedData.Item
        Write-Host "`n🛍️ ITEM EXTRACTION VERIFICATION:" -ForegroundColor Blue
        Write-Host "   Extracted Item: '$extractedItem'" -ForegroundColor White
        Write-Host "   Expected Item: '$ExpectedItem'" -ForegroundColor White
        
        if ($extractedItem.ToLower() -eq $ExpectedItem.ToLower()) {
            Write-Host "   ✅ CORRECT: Item correctly identified!" -ForegroundColor Green
        } elseif ($extractedItem.ToLower() -eq "miscellaneous") {
            Write-Host "   ❌ ISSUE: Item extracted as 'Miscellaneous' instead of specific item" -ForegroundColor Red
        } else {
            Write-Host "   ⚠️  DIFFERENT: Item extracted as '$extractedItem' (expected '$ExpectedItem')" -ForegroundColor Yellow
        }
        
        Write-Host "`n📊 FULL RESPONSE DATA:" -ForegroundColor Cyan
        Write-Host "   Currency: $($response.Currency)" -ForegroundColor White
        Write-Host "   Date: $($response.ExtractedData.Date)" -ForegroundColor White
        Write-Host "   Merchant: $($response.ExtractedData.Merchant)" -ForegroundColor White
        Write-Host "   Message: $($response.Message)" -ForegroundColor White
        
    } catch {
        Write-Host "`n❌ ERROR: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`nStarting server (if not already running)..." -ForegroundColor Yellow
Start-Process -WindowStyle Hidden -FilePath "dotnet" -ArgumentList "run" -WorkingDirectory ".\secondwifeapi" -ErrorAction SilentlyContinue
Start-Sleep -Seconds 6

Write-Host "`nTesting specific issue scenarios:" -ForegroundColor Magenta

# Test the exact example provided by the user
Test-AmountAndItemExtraction -TestName "Test 1: User's Original Issue (Cheese Purchase)" -SpeechText "Got some cheese for 150 rupees, about 200g" -ExpectedAmount 150 -ExpectedItem "cheese"

# Test more specific item extractions
Test-AmountAndItemExtraction -TestName "Test 2: Coffee Purchase" -SpeechText "I bought coffee for 5 dollars at Starbucks" -ExpectedAmount 5 -ExpectedItem "coffee"

Test-AmountAndItemExtraction -TestName "Test 3: Milk Purchase" -SpeechText "Picked up milk for 3.50 dollars" -ExpectedAmount 3.5 -ExpectedItem "milk"

Test-AmountAndItemExtraction -TestName "Test 4: Bread Purchase" -SpeechText "Got bread from the bakery for 2.25 dollars" -ExpectedAmount 2.25 -ExpectedItem "bread"

Test-AmountAndItemExtraction -TestName "Test 5: Gasoline Purchase" -SpeechText "Filled up gas for 45 dollars" -ExpectedAmount 45 -ExpectedItem "gasoline"

Test-AmountAndItemExtraction -TestName "Test 6: Specific Food Item" -SpeechText "Bought apples for 8 dollars" -ExpectedAmount 8 -ExpectedItem "apples"

Write-Host "`n" + "=" * 65
Write-Host "TOTAL AMOUNT AND ITEM EXTRACTION TEST COMPLETED" -ForegroundColor Cyan

Write-Host "`nKey fixes implemented:" -ForegroundColor Green
Write-Host "✓ TotalAmount now shows extracted amount (not accumulated total)" -ForegroundColor Green
Write-Host "✓ Enhanced system message to identify specific items" -ForegroundColor Green
Write-Host "✓ Improved function parameter description for item extraction" -ForegroundColor Green
Write-Host "✓ Response currency now matches extracted currency" -ForegroundColor Green

Write-Host "`nExpected behavior:" -ForegroundColor Yellow
Write-Host "• Response totalAmount should equal extractedData.amount" -ForegroundColor Yellow
Write-Host "• Item should be specific (cheese, coffee) not generic (miscellaneous)" -ForegroundColor Yellow
Write-Host "• Currency should be consistent between extracted and response" -ForegroundColor Yellow