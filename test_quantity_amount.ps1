# Test script for Quantity vs Amount extraction fix

Write-Host "Testing Voice Expense API - Quantity vs Amount Fix" -ForegroundColor Cyan
Write-Host "=" * 55

# Function to test specific speech patterns that might confuse quantity with amount
function Test-QuantityAmountExtraction {
    param(
        [string]$TestName,
        [string]$SpeechText,
        [double]$ExpectedAmount
    )
    
    Write-Host "`n$TestName" -ForegroundColor Yellow
    Write-Host "-" * $TestName.Length -ForegroundColor Yellow
    Write-Host "Speech: `"$SpeechText`"" -ForegroundColor White
    Write-Host "Expected Amount: $ExpectedAmount" -ForegroundColor Green
    
    $body = @{
        UserId = 1
        GroupId = 1
        SpeechText = $SpeechText
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/voice-expense" -Method POST -Body $body -ContentType "application/json"
        
        $extractedAmount = $response.ExtractedData.Amount
        
        if ($extractedAmount -eq $ExpectedAmount) {
            Write-Host "✅ CORRECT: Extracted amount = $extractedAmount" -ForegroundColor Green
        } else {
            Write-Host "❌ INCORRECT: Extracted amount = $extractedAmount (Expected: $ExpectedAmount)" -ForegroundColor Red
            if ($extractedAmount -lt 10 -and $ExpectedAmount -gt 10) {
                Write-Host "   ^ This looks like quantity was extracted instead of amount!" -ForegroundColor Red
            }
        }
        
        Write-Host "   Item: $($response.ExtractedData.Item)" -ForegroundColor Cyan
        Write-Host "   Merchant: $($response.ExtractedData.Merchant)" -ForegroundColor Cyan
        Write-Host "   Currency: $($response.ExtractedData.Currency)" -ForegroundColor Cyan
        
    } catch {
        Write-Host "❌ ERROR: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`nStarting server (if not already running)..." -ForegroundColor Yellow
Start-Process -WindowStyle Hidden -FilePath "dotnet" -ArgumentList "run" -WorkingDirectory ".\secondwifeapi" -ErrorAction SilentlyContinue
Start-Sleep -Seconds 6

Write-Host "`nTesting scenarios that commonly cause quantity/amount confusion:" -ForegroundColor Magenta

# Test cases designed to catch quantity/amount confusion
Test-QuantityAmountExtraction -TestName "Test 1: Multiple items with total cost" -SpeechText "I bought 3 coffees for 15 dollars at Starbucks" -ExpectedAmount 15

Test-QuantityAmountExtraction -TestName "Test 2: Single item clear cost" -SpeechText "I spent 12 dollars on lunch at McDonald's" -ExpectedAmount 12

Test-QuantityAmountExtraction -TestName "Test 3: Quantity mentioned before price" -SpeechText "I got 2 sandwiches and paid 18 dollars" -ExpectedAmount 18

Test-QuantityAmountExtraction -TestName "Test 4: Price per item pattern" -SpeechText "I bought 4 drinks at 3 dollars each for a total of 12 dollars" -ExpectedAmount 12

Test-QuantityAmountExtraction -TestName "Test 5: Simple cost statement" -SpeechText "I paid 25 dollars for groceries" -ExpectedAmount 25

Test-QuantityAmountExtraction -TestName "Test 6: Multiple quantities with total" -SpeechText "I bought 5 apples and 3 oranges for 8 dollars total" -ExpectedAmount 8

Test-QuantityAmountExtraction -TestName "Test 7: European currency test" -SpeechText "I bought 2 books for 30 euros" -ExpectedAmount 30

Write-Host "`n" + "=" * 55
Write-Host "QUANTITY vs AMOUNT EXTRACTION TEST COMPLETED" -ForegroundColor Cyan
Write-Host "`nKey improvements made:" -ForegroundColor Green
Write-Host "✓ Enhanced function description to emphasize TOTAL COST" -ForegroundColor Green
Write-Host "✓ Added system message to clarify money vs quantity" -ForegroundColor Green
Write-Host "✓ Added validation warning for potential confusion" -ForegroundColor Green
Write-Host "✓ Improved parameter descriptions with examples" -ForegroundColor Green