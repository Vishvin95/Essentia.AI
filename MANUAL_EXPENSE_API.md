# Manual Expense API Documentation

## Overview
The Manual Expense API allows users to directly create expense entries without needing to upload and process receipt documents. This is useful for cash transactions, small purchases, or when receipts are not available.

## Endpoint

**POST** `/api/invoice/manual-expense`

Creates a new manual expense entry that will be saved to the same database tables used by the automated invoice processing system.

## Request Format

```json
{
  "UserId": 1,
  "GroupId": 1, 
  "VendorName": "Coffee Shop",
  "ItemName": "Large Latte",
  "Price": 5.50,
  "Currency": "USD",
  "Date": "2024-01-15T10:30:00Z"
}
```

### Request Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `UserId` | integer | ✅ Yes | ID of the user creating the expense (must be > 0) |
| `GroupId` | integer | ✅ Yes | ID of the group this expense belongs to (must be > 0) |
| `VendorName` | string | ✅ Yes | Name of the merchant/vendor/place where purchase was made |
| `ItemName` | string | ✅ Yes | Description of the item/service purchased (cannot be empty) |
| `Price` | decimal | ✅ Yes | Cost of the item (must be > 0) |
| `Currency` | string | ❌ No | ISO 4217 currency code (defaults to "USD") |
| `Date` | datetime | ❌ No | Date of the expense (defaults to current date) |

## Response Format

### Success Response (200 OK)
```json
{
  "Success": true,
  "Message": "Manual expense created successfully",
  "ExpenseId": "1_2024-01-15",
  "TotalAmount": 18.45,
  "Currency": "USD",
  "Date": "2024-01-15T00:00:00Z"
}
```

### Error Response (400 Bad Request)
```json
{
  "Message": "UserId, GroupId, ItemName, and Price are required and must be valid."
}
```

### Error Response (500 Internal Server Error)
```json
{
  "Success": false,
  "Message": "Error creating manual expense."
}
```

## Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `Success` | boolean | Indicates if the operation was successful |
| `Message` | string | Human-readable status message |
| `ExpenseId` | string | Unique identifier in format "{UserId}_{Date}" |
| `TotalAmount` | decimal | Total amount for all expenses on this date for this user |
| `Currency` | string | Currency code used |
| `Date` | datetime | Date of the expense (normalized to midnight) |

## Database Behavior

### Expense Aggregation
- Multiple expenses for the same user on the same date are **aggregated** into a single `Expense` record
- The `TotalAmount` represents the sum of all expenses for that user on that date
- Vendor names are combined (e.g., "Coffee Shop, Deli, Gas Station")

### Individual Items
- Each manual expense creates a separate `ExpenseItem` record
- This maintains the detail of individual purchases while aggregating totals

### Example Database State
After creating these manual expenses:
1. User 1, 2024-01-15: Coffee ($5.50)
2. User 1, 2024-01-15: Lunch ($12.00)  
3. User 1, 2024-01-16: Gas ($45.00)

**Expenses Table:**
| UserId | Date | TotalAmount | VendorName | Currency |
|--------|------|-------------|------------|----------|
| 1 | 2024-01-15 | 17.50 | Coffee Shop, Deli | USD |
| 1 | 2024-01-16 | 45.00 | Gas Station | USD |

**ExpenseItems Table:**
| UserId | Date | Description | Amount | Currency |
|--------|------|-------------|--------|----------|
| 1 | 2024-01-15 | Coffee | 5.50 | USD |
| 1 | 2024-01-15 | Lunch | 12.00 | USD |
| 1 | 2024-01-16 | Gas | 45.00 | USD |

## Integration with Existing System

This API uses the same database tables (`Expenses` and `ExpenseItems`) as the automated invoice processing system, ensuring:

- **Consistent Data Model**: Manual and automated expenses are stored identically
- **Unified Reporting**: All expenses appear together in queries and reports  
- **Same Business Logic**: Date aggregation and currency handling work the same way
- **Audit Trail**: Manual expenses get unique JobIds for tracking

## Usage Examples

### Basic Usage
```bash
curl -X POST "http://localhost:5256/api/invoice/manual-expense" \
  -H "Content-Type: application/json" \
  -d '{
    "UserId": 1,
    "GroupId": 1,
    "VendorName": "Starbucks",
    "ItemName": "Grande Latte",
    "Price": 5.95
  }'
```

### With Currency and Date
```bash
curl -X POST "http://localhost:5256/api/invoice/manual-expense" \
  -H "Content-Type: application/json" \
  -d '{
    "UserId": 1,
    "GroupId": 1,
    "VendorName": "European Cafe",
    "ItemName": "Espresso",
    "Price": 3.50,
    "Currency": "EUR",
    "Date": "2024-01-15T14:30:00Z"
  }'
```

## Validation Rules

- `UserId` must be greater than 0
- `GroupId` must be greater than 0  
- `ItemName` cannot be empty or whitespace
- `Price` must be greater than 0
- `Currency` must be a valid 3-letter ISO code (if provided)
- `Date` must be a valid datetime (if provided)

## Testing

Use the provided test script to verify functionality:
```bash
./test_manual_expense.ps1
```

This script tests:
- ✅ Creating new expenses
- ✅ Expense aggregation (same user/date)
- ✅ Currency handling
- ✅ Default value behavior
- ✅ Input validation