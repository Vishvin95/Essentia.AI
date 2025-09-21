# Voice Expense API Documentation

## Overview

The Voice Expense API allows users to create expenses by providing natural language speech text. The API uses Azure OpenAI (or a mock service for testing) to extract structured expense information from free-form text.

## Endpoint

**POST** `/api/invoice/voice-expense`

## Request Model

```json
{
  "UserId": 1,
  "GroupId": 1,
  "SpeechText": "I spent 12 dollars on coffee at Starbucks today"
}
```

### Request Fields

- `UserId` (required): The ID of the user creating the expense
- `GroupId` (required): The ID of the group the expense belongs to
- `SpeechText` (required): Natural language text describing the expense

## Response Model

```json
{
  "Success": true,
  "Message": "Voice expense created successfully",
  "ExpenseId": "123",
  "ExtractedData": {
    "Amount": 12.0,
    "Item": "Coffee",
    "Date": "2024-01-15",
    "Currency": "USD",  
    "Merchant": "Starbucks"
  },
  "TotalAmount": 12.0,
  "Currency": "USD",
  "Date": "2024-01-15T00:00:00"
}
```

### Response Fields

- `Success`: Boolean indicating if the operation was successful
- `Message`: Human-readable message about the operation
- `ExpenseId`: The ID of the created/updated expense
- `ExtractedData`: The structured data extracted from the speech text
  - `Amount`: The monetary amount
  - `Item`: The expense item/category
  - `Date`: The date of the expense (optional, defaults to today)
  - `Currency`: The currency code (USD, EUR, GBP, etc.)
  - `Merchant`: The merchant/vendor (optional)
- `TotalAmount`: The total amount for the expense (may include aggregated amounts)
- `Currency`: The currency of the expense
- `Date`: The date the expense was recorded

## Example Usage

### Simple Coffee Purchase
```bash
curl -X POST "http://localhost:5256/api/invoice/voice-expense" \
  -H "Content-Type: application/json" \
  -d '{
    "UserId": 1,
    "GroupId": 1,
    "SpeechText": "I spent 12 dollars on coffee at Starbucks today"
  }'
```

### Grocery Shopping with Date
```bash
curl -X POST "http://localhost:5256/api/invoice/voice-expense" \
  -H "Content-Type: application/json" \
  -d '{
    "UserId": 2,
    "GroupId": 1,
    "SpeechText": "Yesterday I bought groceries for 45.50 euros at the local supermarket"
  }'
```

### Restaurant Meal
```bash
curl -X POST "http://localhost:5256/api/invoice/voice-expense" \
  -H "Content-Type: application/json" \
  -d '{
    "UserId": 1,
    "GroupId": 2,
    "SpeechText": "I paid 25 pounds for lunch at the restaurant"
  }'
```

## Supported Speech Patterns

The AI extraction service can understand various natural language patterns:

- **Amount**: "12 dollars", "$12", "12.50", "twenty dollars"
- **Currency**: "$", "dollars", "USD", "€", "euros", "EUR", "£", "pounds", "GBP"
- **Items**: "coffee", "lunch", "groceries", "gas", "dinner"
- **Merchants**: "Starbucks", "the restaurant", "local supermarket"
- **Dates**: "today", "yesterday", "tomorrow", "2024-01-15"

## Configuration

### Azure OpenAI (Production)
To use Azure OpenAI for production, configure in `appsettings.json`:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-openai-resource.openai.azure.com/",
    "ApiKey": "your-api-key-here",
    "DeploymentName": "your-deployment-name"
  }
}
```

And update `Program.cs`:
```csharp
// For production with Azure OpenAI:
builder.Services.AddHttpClient<IExpenseExtractionService, AzureOpenAIExtractionService>();
```

### Mock Service (Testing)
For testing without Azure OpenAI, the mock service is used by default:

```csharp
// For testing with mock service:
builder.Services.AddScoped<IExpenseExtractionService, MockExpenseExtractionService>();
```

## Error Handling

The API returns appropriate HTTP status codes:

- **200 OK**: Expense created successfully
- **400 Bad Request**: Invalid request data or failed to extract expense information
- **500 Internal Server Error**: Server error during processing

Example error response:
```json
{
  "Success": false,
  "Message": "Failed to extract expense information from speech text."
}
```

## Database Schema Integration

The voice expense API integrates with the updated database schema:

- Creates entries in the `Expenses` table with `ExpenseId` as primary key
- Links expenses to users via `UserId` and groups via `GroupId`
- Creates detailed items in the `ExpenseItems` table
- Supports expense aggregation (multiple items for same user/group/date)

## Implementation Details

### Services Used

1. **IExpenseExtractionService**: Interface for expense extraction
2. **AzureOpenAIExtractionService**: Production implementation using Azure OpenAI
3. **MockExpenseExtractionService**: Testing implementation with pattern matching

### Data Flow

1. User provides natural language text
2. Extraction service processes the text and returns structured data
3. Controller validates the extracted data
4. Expense and ExpenseItem records are created/updated in the database
5. Response is returned with expense details and extracted information

### Currency Normalization

The service normalizes various currency formats to standard 3-letter codes:
- "$" → "USD"
- "€" → "EUR" 
- "£" → "GBP"
- "¥" → "JPY"
- etc.

### Date Processing

Date references are converted to standard format:
- "today" → Current date
- "yesterday" → Previous day
- "tomorrow" → Next day
- ISO dates are parsed directly
- Defaults to current date if no date specified