# Test script for the currency functionality

Write-Host "Testing Currency Functionality" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green

# 1. Test user creation with custom currency
Write-Host "`n1. Testing user creation with custom currency (EUR)..." -ForegroundColor Yellow
$signUpBody = @{
    Username = "testuser_eur_$(Get-Random)"
    Password = "TestPassword123!"
    Email = "testuser_eur@example.com"
    DisplayName = "Test User EUR"
    DefaultCurrency = "EUR"
} | ConvertTo-Json

Write-Host "Request body: $signUpBody"

try {
    $signUpResponse = Invoke-RestMethod -Uri "http://localhost:5256/api/userauthentication/sign-up" -Method POST -Body $signUpBody -ContentType "application/json"
    Write-Host "SignUp response: $($signUpResponse | ConvertTo-Json -Depth 3)" -ForegroundColor Green
    $userId = $signUpResponse.UserId
    Write-Host "Created user with ID: $userId and default currency: EUR"
} catch {
    Write-Host "Error creating user with EUR currency: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)"
    }
}

Write-Host "`n" + "="*50

# 2. Test user creation with invalid currency
Write-Host "`n2. Testing user creation with invalid currency (XYZ)..." -ForegroundColor Yellow
$invalidCurrencyBody = @{
    Username = "testuser_invalid_$(Get-Random)"
    Password = "TestPassword123!"
    Email = "testuser_invalid@example.com"
    DisplayName = "Test User Invalid"
    DefaultCurrency = "XYZ"
} | ConvertTo-Json

try {
    $invalidResponse = Invoke-RestMethod -Uri "http://localhost:5256/api/userauthentication/sign-up" -Method POST -Body $invalidCurrencyBody -ContentType "application/json"
    Write-Host "Invalid currency response: $($invalidResponse | ConvertTo-Json -Depth 3)" -ForegroundColor Red
} catch {
    Write-Host "Expected error for invalid currency: $($_.Exception.Message)" -ForegroundColor Green
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)"
    }
}

Write-Host "`n" + "="*50

# 3. Test user creation with default currency (USD)
Write-Host "`n3. Testing user creation with default currency (USD)..." -ForegroundColor Yellow
$defaultCurrencyBody = @{
    Username = "testuser_usd_$(Get-Random)"
    Password = "TestPassword123!"
    Email = "testuser_usd@example.com"
    DisplayName = "Test User USD"
    DefaultCurrency = "USD"
} | ConvertTo-Json

try {
    $usdResponse = Invoke-RestMethod -Uri "http://localhost:5256/api/userauthentication/sign-up" -Method POST -Body $defaultCurrencyBody -ContentType "application/json"
    Write-Host "USD user response: $($usdResponse | ConvertTo-Json -Depth 3)" -ForegroundColor Green
    $usdUserId = $usdResponse.UserId
} catch {
    Write-Host "Error creating USD user: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n" + "="*50

# 4. Test save-expense API (will now use user's default currency for fallback)
Write-Host "`n4. Testing save-expense API with currency fallback..." -ForegroundColor Yellow
if ($usdUserId) {
    $saveExpenseBody = @{
        GroupId = 1
        UserId = [int]$usdUserId
        BlobSasUrl = "https://vvinamlworkspa3466416834.blob.core.windows.net/invoice-blob/test-invoice.pdf?sv=2022-11-02&sr=b&sig=test123&se=2024-01-01T00:00:00Z"
    } | ConvertTo-Json

    Write-Host "Save expense request: $saveExpenseBody"

    try {
        $expenseResponse = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/save-expense" -Method POST -Body $saveExpenseBody -ContentType "application/json"
        Write-Host "Save expense response: $($expenseResponse | ConvertTo-Json -Depth 3)" -ForegroundColor Green
        $jobId = $expenseResponse.JobId
    } catch {
        Write-Host "Error saving expense: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n" + "="*50

# 5. Test webhook callback with currency information
Write-Host "`n5. Testing webhook callback with currency data..." -ForegroundColor Yellow
$webhookWithCurrencyBody = @{
    JobId = "test-job-currency-$(Get-Random)"
    GroupId = 1
    UserId = 1
    BlobSasUrl = "https://example.com/test.pdf"
    Status = "completed"
    ProcessedAt = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
    InvoiceData = @{
        VendorName = "European Vendor Ltd"
        InvoiceTotal = 450.75
        Currency = "EUR"
        InvoiceDate = "2024-01-15"
        Items = @(
            @{
                Description = "Software License"
                Amount = 400.00
                Currency = "EUR"
                Quantity = 1
            },
            @{
                Description = "Support Fee"
                Amount = 50.75
                Currency = "EUR"
                Quantity = 1
            }
        )
    }
} | ConvertTo-Json -Depth 5

Write-Host "Webhook with currency request: $webhookWithCurrencyBody"

try {
    $currencyWebhookResponse = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/webhook-callback" -Method POST -Body $webhookWithCurrencyBody -ContentType "application/json"
    Write-Host "Currency webhook response: $($currencyWebhookResponse | ConvertTo-Json -Depth 3)" -ForegroundColor Green
} catch {
    Write-Host "Error calling currency webhook: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)"
    }
}

Write-Host "`n" + "="*50
Write-Host "`nCurrency functionality testing completed!" -ForegroundColor Green

# Summary of supported currencies
Write-Host "`nSupported Currency Codes:" -ForegroundColor Cyan
Write-Host "USD, EUR, GBP, JPY, AUD, CAD, CHF, CNY, SEK, NZD," -ForegroundColor White
Write-Host "MXN, SGD, HKD, NOK, TRY, ZAR, BRL, INR, KRW, PLN," -ForegroundColor White
Write-Host "DKK, CZK, HUF, ILS, CLP, PHP, AED, COP, SAR, MYR," -ForegroundColor White
Write-Host "RON, THB, BGN, HRK, RUB, ISK, IDR, UAH" -ForegroundColor White