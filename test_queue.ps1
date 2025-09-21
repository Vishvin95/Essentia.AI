# Test Azure Storage Queue connectivity
$body = @{
    GroupId = 1
    UserId = 1
    BlobSasUrl = "https://vvinamlworkspa3466416834.blob.core.windows.net/invoice-blob/test-invoice.pdf?sv=2022-11-02&sr=b&sig=test123&se=2024-01-01T00:00:00Z"
} | ConvertTo-Json

Write-Host "Testing save-expense API..."
Write-Host "Request body: $body"

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5256/api/invoice/save-expense" -Method POST -Body $body -ContentType "application/json"
    Write-Host "Response: $($response | ConvertTo-Json -Depth 3)"
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    Write-Host "Status Code: $($_.Exception.Response.StatusCode)"
}