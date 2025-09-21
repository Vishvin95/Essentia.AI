# Test script for webhook callback functionality (database persistence)

Write-Host "="*60
Write-Host "TESTING WEBHOOK CALLBACK WITH DATABASE PERSISTENCE"
Write-Host "="*60

# Test webhook callback API (simulating what the background service would send)
Write-Host "Testing webhook callback with complete invoice data..."

$webhookBody = @{
    JobId = "test-expense-db-001"
    GroupId = 1
    UserId = 1
    BlobSasUrl = "https://example.com/test-receipt.pdf"
    Status = "completed"
    ProcessedAt = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
    InvoiceData = @{
        VendorName = "Coffee Shop & Bakery"
        InvoiceTotal = 156.75
        Currency = "USD"
        InvoiceDate = "2024-01-15T08:30:00Z"
        Items = @(
            @{
                Description = "Espresso Coffee (Large)"
                Amount = 5.50
                Currency = "USD"
                Quantity = 2
            },
            @{
                Description = "Blueberry Muffin"
                Amount = 3.25
                Currency = "USD"
                Quantity = 3
            },
            @{
                Description = "Meeting Room Rental"
                Amount = 125.00
                Currency = "USD"
                Quantity = 1
            },
            @{
                Description = "Service Charge"
                Amount = 7.75
                Currency = "USD"
                Quantity = 1
            }
        )
    }
} | ConvertTo-Json -Depth 5

Write-Host "`nWebhook request payload:"
Write-Host $webhookBody
Write-Host "`n" + "-"*50

try {
    $webhookResponse = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/webhook-callback" -Method POST -Body $webhookBody -ContentType "application/json"
    Write-Host "✅ SUCCESS: Webhook callback processed successfully!" -ForegroundColor Green
    Write-Host "Response: $($webhookResponse | ConvertTo-Json -Depth 3)" -ForegroundColor Green
    
    Write-Host "`n📊 Expected Database Changes:"
    Write-Host "  • Expense record created for User 1 on 2024-01-15"
    Write-Host "  • Total Amount: $156.75 USD"
    Write-Host "  • Vendor: Coffee Shop & Bakery"
    Write-Host "  • 4 ExpenseItem records created"
    Write-Host "  • JobId: test-expense-db-001 stored as reference"
    
} catch {
    Write-Host "❌ ERROR: Webhook callback failed!" -ForegroundColor Red
    Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "HTTP Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
        try {
            $errorContent = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($errorContent)
            $errorBody = $reader.ReadToEnd()
            Write-Host "Error details: $errorBody" -ForegroundColor Red
        } catch {
            Write-Host "Could not read error details" -ForegroundColor Red
        }
    }
}

Write-Host "`n" + "="*50 + "`n"

# Test webhook callback with existing expense (same user, same date)
Write-Host "Testing webhook callback with EXISTING expense (same user, same date)..."

$existingExpenseWebhook = @{
    JobId = "test-expense-db-002"
    GroupId = 1
    UserId = 1  # Same user
    BlobSasUrl = "https://example.com/dinner-receipt.pdf"
    Status = "completed"
    ProcessedAt = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
    InvoiceData = @{
        VendorName = "Italian Restaurant"
        InvoiceTotal = 89.50
        Currency = "USD"
        InvoiceDate = "2024-01-15T19:30:00Z"  # Same date, different time
        Items = @(
            @{
                Description = "Pasta Primavera"
                Amount = 24.50
                Currency = "USD"
                Quantity = 1
            },
            @{
                Description = "Caesar Salad"
                Amount = 18.00
                Currency = "USD"
                Quantity = 1
            },
            @{
                Description = "Wine (Bottle)"
                Amount = 35.00
                Currency = "USD"
                Quantity = 1
            },
            @{
                Description = "Tip"
                Amount = 12.00
                Currency = "USD"
                Quantity = 1
            }
        )
    }
} | ConvertTo-Json -Depth 5

try {
    $existingResponse = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/webhook-callback" -Method POST -Body $existingExpenseWebhook -ContentType "application/json"
    Write-Host "✅ SUCCESS: Existing expense updated!" -ForegroundColor Green
    Write-Host "Response: $($existingResponse | ConvertTo-Json -Depth 3)" -ForegroundColor Green
    
    Write-Host "`n📊 Expected Database Changes:"
    Write-Host "  • Existing Expense record UPDATED for User 1 on 2024-01-15"
    Write-Host "  • New Total Amount: $246.25 USD ($156.75 + $89.50)"
    Write-Host "  • Updated Vendor: 'Coffee Shop & Bakery, Italian Restaurant'"
    Write-Host "  • 4 additional ExpenseItem records created"
    
} catch {
    Write-Host "❌ ERROR: Existing expense webhook failed!" -ForegroundColor Red
    Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n" + "="*50 + "`n"

# Test webhook callback with failed status (should not save to database)
Write-Host "Testing webhook callback with FAILED status (should not save to DB)..."

$failedWebhook = @{
    JobId = "test-expense-db-003"
    GroupId = 1
    UserId = 1
    BlobSasUrl = "https://example.com/corrupted-receipt.pdf"
    Status = "failed"
    ProcessedAt = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
    Error = "Document analysis failed: Corrupted file or unsupported format"
} | ConvertTo-Json -Depth 3

try {
    $failedResponse = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/webhook-callback" -Method POST -Body $failedWebhook -ContentType "application/json"
    Write-Host "✅ SUCCESS: Failed webhook processed (no database changes expected)" -ForegroundColor Green
    Write-Host "Response: $($failedResponse | ConvertTo-Json -Depth 3)" -ForegroundColor Green
    
    Write-Host "`n📊 Expected Database Changes:"
    Write-Host "  • NO expense or expense item records created"
    Write-Host "  • Error logged for JobId: test-expense-db-003"
    
} catch {
    Write-Host "❌ ERROR: Failed webhook processing failed!" -ForegroundColor Red
    Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n" + "="*60
Write-Host "DATABASE PERSISTENCE TEST COMPLETED"
Write-Host "="*60