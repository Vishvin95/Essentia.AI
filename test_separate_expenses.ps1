# Test script for Separate Expense Creation Logic

Write-Host "Testing Voice Expense API - Separate Expense Creation" -ForegroundColor Cyan
Write-Host "=" * 55

# Function to test expense creation
function Test-SeparateExpenseCreation {
    param(
        [string]$TestName,
        [int]$UserId,
        [int]$GroupId,
        [string]$SpeechText
    )
    
    Write-Host "`n$TestName" -ForegroundColor Yellow
    Write-Host "-" * $TestName.Length -ForegroundColor Yellow
    Write-Host "User: $UserId, Group: $GroupId" -ForegroundColor White
    Write-Host "Speech: `"$SpeechText`"" -ForegroundColor White
    
    $body = @{
        UserId = $UserId
        GroupId = $GroupId
        SpeechText = $SpeechText
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/voice-expense" -Method POST -Body $body -ContentType "application/json"
        
        Write-Host "✅ SUCCESS: New expense created!" -ForegroundColor Green
        Write-Host "   ExpenseId: $($response.ExpenseId)" -ForegroundColor Yellow
        Write-Host "   Amount: $($response.ExtractedData.Amount) $($response.ExtractedData.Currency)" -ForegroundColor Cyan
        Write-Host "   Item: $($response.ExtractedData.Item)" -ForegroundColor Cyan
        Write-Host "   Merchant: $($response.ExtractedData.Merchant)" -ForegroundColor Cyan
        Write-Host "   Total Amount: $($response.TotalAmount) $($response.Currency)" -ForegroundColor Green
        
        return $response.ExpenseId
        
    } catch {
        Write-Host "❌ ERROR: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

Write-Host "`nStarting server (if not already running)..." -ForegroundColor Yellow
Start-Process -WindowStyle Hidden -FilePath "dotnet" -ArgumentList "run" -WorkingDirectory ".\secondwifeapi" -ErrorAction SilentlyContinue
Start-Sleep -Seconds 6

Write-Host "`nTesting that each voice entry creates a separate expense:" -ForegroundColor Magenta

# Test 1: Same user, same group, same date - should create separate expenses
Write-Host "`n🧪 TEST SCENARIO 1: Same User, Same Group, Same Date (should be separate)" -ForegroundColor Blue
$expense1 = Test-SeparateExpenseCreation -TestName "Coffee Purchase" -UserId 1 -GroupId 1 -SpeechText "I bought coffee for 5 dollars at Starbucks"
$expense2 = Test-SeparateExpenseCreation -TestName "Lunch Purchase (Same User/Group/Date)" -UserId 1 -GroupId 1 -SpeechText "I got lunch for 12 dollars at McDonald's"

if ($expense1 -ne $expense2 -and $expense1 -ne $null -and $expense2 -ne $null) {
    Write-Host "✅ CORRECT: Different ExpenseIds ($expense1 vs $expense2) - Separate expenses created!" -ForegroundColor Green
} else {
    Write-Host "❌ ISSUE: Same ExpenseId or null - Expenses might be getting accumulated!" -ForegroundColor Red
}

# Test 2: Same user, different groups, same date - should definitely be separate
Write-Host "`n🧪 TEST SCENARIO 2: Same User, Different Groups, Same Date (should be separate)" -ForegroundColor Blue
$expense3 = Test-SeparateExpenseCreation -TestName "Personal Expense (Group 1)" -UserId 2 -GroupId 1 -SpeechText "I spent 20 dollars on groceries"
$expense4 = Test-SeparateExpenseCreation -TestName "Work Expense (Group 2)" -UserId 2 -GroupId 2 -SpeechText "I bought office supplies for 15 dollars"

if ($expense3 -ne $expense4 -and $expense3 -ne $null -and $expense4 -ne $null) {
    Write-Host "✅ CORRECT: Different ExpenseIds ($expense3 vs $expense4) - Separate expenses for different groups!" -ForegroundColor Green
} else {
    Write-Host "❌ ISSUE: Same ExpenseId or null - This should definitely be separate!" -ForegroundColor Red
}

# Test 3: Multiple expenses for same user to show granular tracking
Write-Host "`n🧪 TEST SCENARIO 3: Multiple Expenses for Granular Tracking" -ForegroundColor Blue
$expense5 = Test-SeparateExpenseCreation -TestName "Gas Station" -UserId 3 -GroupId 1 -SpeechText "Filled up gas for 45 dollars"
$expense6 = Test-SeparateExpenseCreation -TestName "Snacks" -UserId 3 -GroupId 1 -SpeechText "Bought snacks for 8 dollars"
$expense7 = Test-SeparateExpenseCreation -TestName "Parking" -UserId 3 -GroupId 1 -SpeechText "Paid 3 dollars for parking"

Write-Host "   Gas ExpenseId: $expense5" -ForegroundColor Yellow
Write-Host "   Snacks ExpenseId: $expense6" -ForegroundColor Yellow
Write-Host "   Parking ExpenseId: $expense7" -ForegroundColor Yellow

$allDifferent = ($expense5 -ne $expense6) -and ($expense6 -ne $expense7) -and ($expense5 -ne $expense7)
if ($allDifferent -and $expense5 -ne $null -and $expense6 -ne $null -and $expense7 -ne $null) {
    Write-Host "✅ EXCELLENT: All three expenses have different IDs - Perfect granular tracking!" -ForegroundColor Green
} else {
    Write-Host "❌ ISSUE: Some expenses share IDs - Missing granular tracking!" -ForegroundColor Red
}

Write-Host "`n" + "=" * 55
Write-Host "SEPARATE EXPENSE CREATION TEST COMPLETED" -ForegroundColor Cyan

Write-Host "`n📊 BENEFITS OF NEW APPROACH:" -ForegroundColor Green
Write-Host "✓ Each voice entry = separate expense (better tracking)" -ForegroundColor Green
Write-Host "✓ User can see individual transactions clearly" -ForegroundColor Green
Write-Host "✓ Different groups on same date = separate expenses" -ForegroundColor Green
Write-Host "✓ Better data granularity for reporting" -ForegroundColor Green
Write-Host "✓ More intuitive user experience" -ForegroundColor Green

Write-Host "`n🔍 WHAT TO VERIFY IN DATABASE:" -ForegroundColor Yellow
Write-Host "• Each ExpenseId should correspond to one voice input" -ForegroundColor Yellow
Write-Host "• No amount accumulation between separate voice entries" -ForegroundColor Yellow
Write-Host "• Each expense has its own expense items" -ForegroundColor Yellow
Write-Host "• Different groups always get separate expense records" -ForegroundColor Yellow