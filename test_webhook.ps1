# Test script for the webhook functionality

# 1. Test the save-expense API (this will automatically generate the webhook URL)
Write-Host "Testing save-expense API..."
$saveExpenseBody = @{
    GroupId = 1
    UserId = 1
    BlobSasUrl = "https://vvinamlworkspa3466416834.blob.core.windows.net/invoice-blob/test-invoice.pdf?sv=2022-11-02&sr=b&sig=test123&se=2024-01-01T00:00:00Z"
} | ConvertTo-Json

Write-Host "Request body: $saveExpenseBody"

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/save-expense" -Method POST -Body $saveExpenseBody -ContentType "application/json"
    Write-Host "Save-expense response: $($response | ConvertTo-Json -Depth 3)"
    $jobId = $response.JobId
    Write-Host "Generated JobId: $jobId"
} catch {
    Write-Host "Error calling save-expense: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)"
    }
}

Write-Host "`n" + "="*50 + "`n"

# 2. Test the webhook callback API (simulating what the background service would send)
Write-Host "Testing webhook callback API..."
$webhookBody = @{
    JobId = "test-job-123"
    GroupId = 1
    UserId = 1
    BlobSasUrl = "https://example.com/test.pdf"
    Status = "completed"
    ProcessedAt = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
    InvoiceData = @{
        VendorName = "Test Vendor"
        InvoiceTotal = 123.45
        InvoiceDate = "2024-01-15"
        Items = @(
            @{
                Description = "Test Item 1"
                Amount = 100.00
                Quantity = 1
            },
            @{
                Description = "Test Item 2"
                Amount = 23.45
                Quantity = 2
            }
        )
    }
} | ConvertTo-Json -Depth 5

Write-Host "Webhook request body: $webhookBody"

try {
    $webhookResponse = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/webhook-callback" -Method POST -Body $webhookBody -ContentType "application/json"
    Write-Host "Webhook response: $($webhookResponse | ConvertTo-Json -Depth 3)"
} catch {
    Write-Host "Error calling webhook: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)"
    }
}

Write-Host "`n" + "="*50 + "`n"

# 3. Test the webhook callback API with failed status
Write-Host "Testing webhook callback API with failed status..."
$failedWebhookBody = @{
    JobId = "test-job-456"
    GroupId = 1
    UserId = 1
    BlobSasUrl = "https://example.com/test.pdf"
    Status = "failed"
    ProcessedAt = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
    Error = "Document analysis failed: Invalid file format"
} | ConvertTo-Json -Depth 3

Write-Host "Failed webhook request body: $failedWebhookBody"

try {
    $failedWebhookResponse = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/webhook-callback" -Method POST -Body $failedWebhookBody -ContentType "application/json"
    Write-Host "Failed webhook response: $($failedWebhookResponse | ConvertTo-Json -Depth 3)"
} catch {
    Write-Host "Error calling failed webhook: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)"
    }
}