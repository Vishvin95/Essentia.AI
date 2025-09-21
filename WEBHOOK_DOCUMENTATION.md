# Webhook Implementation Documentation

## Overview
The webhook functionality allows the background expense processing service to call back to the InvoiceController when job processing is completed (successfully or with errors).

## Implementation Details

### 1. Components Added/Modified

#### New Models (`WebhookCallbackModels.cs`)
- `WebhookCallbackRequest` - Data sent from background service to webhook endpoint
- `InvoiceDataResult` - Extracted invoice data structure
- `InvoiceItem` - Individual invoice line items
- `WebhookCallbackResponse` - Response from webhook endpoint

#### New API Endpoint
**POST** `/api/invoice/webhook-callback`
- Receives callbacks from the background processing service
- Logs processing results
- Provides extension points for additional logic (database updates, notifications, etc.)

#### Enhanced save-expense API
- Automatically generates webhook URL if not provided in request
- Uses current request's scheme and host to build callback URL
- Always includes webhook URL in queue message

### 2. Flow Description

1. **Client calls save-expense API**
   ```
   POST /api/invoice/save-expense
   {
     "GroupId": 1,
     "UserId": 1,
     "BlobSasUrl": "https://...",
     "WebhookUrl": "optional - auto-generated if not provided"
   }
   ```

2. **API generates webhook URL automatically**
   ```
   Auto-generated: http://localhost:5256/api/invoice/webhook-callback
   ```

3. **Message queued with webhook URL**
   ```json
   {
     "JobId": "guid",
     "GroupId": 1,
     "UserId": 1,
     "BlobSasUrl": "https://...",
     "WebhookUrl": "http://localhost:5256/api/invoice/webhook-callback",
     "CreatedAt": "2024-..."
   }
   ```

4. **Background service processes invoice**
   - Analyzes document using Azure Document Intelligence
   - Extracts invoice data (vendor, total, date, items)

5. **Background service calls webhook on completion**
   - **Success case**: Sends extracted invoice data
   - **Failure case**: Sends error information

6. **Webhook endpoint receives callback**
   - Logs the results
   - Can be extended to update database, send notifications, etc.

### 3. Example Webhook Payloads

#### Successful Processing
```json
{
  "JobId": "12345-guid",
  "GroupId": 1,
  "UserId": 1,
  "BlobSasUrl": "https://...",
  "Status": "completed",
  "ProcessedAt": "2024-01-15T10:30:00Z",
  "InvoiceData": {
    "VendorName": "Acme Corp",
    "InvoiceTotal": 123.45,
    "InvoiceDate": "2024-01-10",
    "Items": [
      {
        "Description": "Widget A",
        "Amount": 100.00,
        "Quantity": 1
      },
      {
        "Description": "Service Fee",
        "Amount": 23.45,
        "Quantity": 1
      }
    ]
  }
}
```

#### Failed Processing
```json
{
  "JobId": "12345-guid",
  "GroupId": 1,
  "UserId": 1,
  "BlobSasUrl": "https://...",
  "Status": "failed",
  "ProcessedAt": "2024-01-15T10:30:00Z",
  "Error": "Document analysis failed: Invalid file format"
}
```

### 4. Extension Points

The webhook endpoint provides several places where you can add custom logic:

#### For Successful Processing
```csharp
// Store extracted data in database
// Update expense records
// Send notifications to users
// Trigger additional workflows
```

#### For Failed Processing
```csharp
// Update job status in database
// Send error notifications to users
// Trigger retry mechanisms
// Handle error workflows
```

### 5. Testing

Use the provided `test_webhook.ps1` script to test:
1. Save-expense API with auto-generated webhook URL
2. Webhook callback with successful processing data
3. Webhook callback with failed processing data

### 6. Configuration

No additional configuration required. The webhook URL is automatically generated based on the current request context.

### 7. Logging

The implementation includes comprehensive logging for:
- Webhook URL generation
- Callback reception
- Processing results
- Error handling

All logs include JobId for easy correlation and debugging.