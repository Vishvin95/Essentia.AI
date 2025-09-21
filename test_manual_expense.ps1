# Test script for Manual Expense API

Write-Host "="*60
Write-Host "TESTING MANUAL EXPENSE API"
Write-Host "="*60

# Test 1: Create a new manual expense
Write-Host "Test 1: Creating a new manual expense..."

$manualExpense1 = @{
    UserId = 1
    GroupId = 1
    VendorName = "Starbucks Coffee"
    ItemName = "Large Cappuccino"
    Price = 5.95
    Currency = "USD"
    Date = "2024-01-15T10:30:00Z"
} | ConvertTo-Json

Write-Host "Request payload:"
Write-Host $manualExpense1
Write-Host "`n" + "-"*50

try {
    $response1 = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/manual-expense" -Method POST -Body $manualExpense1 -ContentType "application/json"
    Write-Host "✅ SUCCESS: Manual expense created!" -ForegroundColor Green
    Write-Host "Response: $($response1 | ConvertTo-Json -Depth 3)" -ForegroundColor Green
} catch {
    Write-Host "❌ ERROR: Failed to create manual expense!" -ForegroundColor Red
    Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "HTTP Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
}

Write-Host "`n" + "="*50 + "`n"

# Test 2: Add another expense for the same user on the same date (should aggregate)
Write-Host "Test 2: Adding another expense for same user/date (should aggregate)..."

$manualExpense2 = @{
    UserId = 1
    GroupId = 1
    VendorName = "Local Deli"
    ItemName = "Turkey Sandwich"
    Price = 12.50
    Currency = "USD"
    Date = "2024-01-15T12:30:00Z"  # Same date as first expense
} | ConvertTo-Json

Write-Host "Request payload:"
Write-Host $manualExpense2
Write-Host "`n" + "-"*50

try {
    $response2 = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/manual-expense" -Method POST -Body $manualExpense2 -ContentType "application/json"
    Write-Host "✅ SUCCESS: Manual expense aggregated!" -ForegroundColor Green
    Write-Host "Response: $($response2 | ConvertTo-Json -Depth 3)" -ForegroundColor Green
    Write-Host "`n📊 Expected aggregation: $5.95 + $12.50 = $18.45" -ForegroundColor Cyan
} catch {
    Write-Host "❌ ERROR: Failed to aggregate expense!" -ForegroundColor Red
    Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n" + "="*50 + "`n"

# Test 3: Create expense with different currency
Write-Host "Test 3: Creating expense with EUR currency..."

$manualExpense3 = @{
    UserId = 2
    GroupId = 1
    VendorName = "European Cafe"
    ItemName = "Croissant and Coffee"
    Price = 8.50
    Currency = "EUR"
    Date = "2024-01-16T09:00:00Z"
} | ConvertTo-Json

Write-Host "Request payload:"
Write-Host $manualExpense3
Write-Host "`n" + "-"*50

try {
    $response3 = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/manual-expense" -Method POST -Body $manualExpense3 -ContentType "application/json"
    Write-Host "✅ SUCCESS: EUR expense created!" -ForegroundColor Green
    Write-Host "Response: $($response3 | ConvertTo-Json -Depth 3)" -ForegroundColor Green
} catch {
    Write-Host "❌ ERROR: Failed to create EUR expense!" -ForegroundColor Red
    Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n" + "="*50 + "`n"

# Test 4: Create expense without optional fields (should use defaults)
Write-Host "Test 4: Creating expense with minimal fields (defaults)..."

$manualExpense4 = @{
    UserId = 3
    GroupId = 2
    VendorName = "Corner Store"
    ItemName = "Bottled Water"
    Price = 1.99
    # No Currency (should default to USD)
    # No Date (should default to today)
} | ConvertTo-Json

Write-Host "Request payload:"
Write-Host $manualExpense4
Write-Host "`n" + "-"*50

try {
    $response4 = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/manual-expense" -Method POST -Body $manualExpense4 -ContentType "application/json"
    Write-Host "✅ SUCCESS: Default expense created!" -ForegroundColor Green
    Write-Host "Response: $($response4 | ConvertTo-Json -Depth 3)" -ForegroundColor Green
    Write-Host "`n📊 Expected defaults: Currency=USD, Date=Today" -ForegroundColor Cyan
} catch {
    Write-Host "❌ ERROR: Failed to create default expense!" -ForegroundColor Red
    Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n" + "="*50 + "`n"

# Test 5: Test validation with invalid data
Write-Host "Test 5: Testing validation with invalid data..."

$invalidExpense = @{
    UserId = 0  # Invalid
    GroupId = 1
    VendorName = "Test Vendor"
    ItemName = ""  # Invalid (empty)
    Price = -5.00  # Invalid (negative)
    Currency = "USD"
} | ConvertTo-Json

Write-Host "Request payload (should fail validation):"
Write-Host $invalidExpense
Write-Host "`n" + "-"*50

try {
    $response5 = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/manual-expense" -Method POST -Body $invalidExpense -ContentType "application/json"
    Write-Host "❌ UNEXPECTED: Invalid data was accepted!" -ForegroundColor Yellow
    Write-Host "Response: $($response5 | ConvertTo-Json -Depth 3)" -ForegroundColor Yellow
} catch {
    Write-Host "✅ SUCCESS: Validation correctly rejected invalid data!" -ForegroundColor Green
    Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Green
    if ($_.Exception.Response) {
        Write-Host "HTTP Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Green
    }
}

Write-Host "`n" + "="*60
Write-Host "MANUAL EXPENSE API TESTS COMPLETED"
Write-Host "="*60

Write-Host "`n📊 Summary of expected database state:"
Write-Host "  • User 1, Date 2024-01-15: 2 expenses aggregated ($18.45 USD)"
Write-Host "  • User 2, Date 2024-01-16: 1 expense (€8.50 EUR)"
Write-Host "  • User 3, Date Today: 1 expense ($1.99 USD)"
Write-Host "  • Invalid expense: Rejected (no database entry)"