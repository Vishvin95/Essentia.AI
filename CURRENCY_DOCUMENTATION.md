# Currency Support Documentation

## Overview
The expense processing system now includes comprehensive currency support, allowing users to set their default currency and automatically extracting currency information from invoice documents.

## Key Features

### 1. User Default Currency
- Each user has a `DefaultCurrency` field stored in the database
- Uses ISO 4217 currency codes (3-letter format: USD, EUR, GBP, etc.)
- Default value: "USD" if not specified
- Validates currency codes during user creation

### 2. Currency Extraction from Documents
- Attempts to extract currency information from invoices using Azure Document Intelligence
- Supports multiple extraction methods:
  - Currency symbols ($, €, £, ¥, etc.)
  - ISO currency codes in document text
  - Currency information from amount fields
- Falls back to user's default currency if extraction fails

### 3. Multi-Level Currency Support
- **Invoice Level**: Overall invoice currency
- **Item Level**: Individual line item currencies (can differ from invoice currency)
- **Fallback Logic**: User default → Document extracted → USD

## Implementation Details

### Database Changes
```sql
-- Migration: AddDefaultCurrencyToUser
ALTER TABLE [Users] ADD [DefaultCurrency] nvarchar(3) NOT NULL DEFAULT N'USD';
```

### Updated Models

#### User Model
```csharp
public class User
{
    // ... existing properties
    public string DefaultCurrency { get; set; } = "USD"; // ISO 4217 currency code
}
```

#### SignUpRequest Model
```csharp
public class SignUpRequest
{
    // ... existing properties
    public string DefaultCurrency { get; set; } = "USD"; // ISO 4217 currency code
}
```

#### Invoice Data Models
```csharp
public class InvoiceDataResult
{
    public string? VendorName { get; set; }
    public decimal? InvoiceTotal { get; set; }
    public string? Currency { get; set; } // NEW: ISO 4217 currency code
    public DateTime? InvoiceDate { get; set; }
    public List<InvoiceItem>? Items { get; set; }
}

public class InvoiceItem
{
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; } // NEW: ISO 4217 currency code
    public double? Quantity { get; set; }
}
```

### API Changes

#### User Creation (`POST /api/userauthentication/sign-up`)
```json
{
  "Username": "john_doe",
  "Password": "SecurePassword123!",
  "Email": "john@example.com",
  "DisplayName": "John Doe",
  "DefaultCurrency": "EUR"  // NEW: Optional, defaults to "USD"
}
```

#### Currency Validation
- Validates against list of 38 supported currency codes
- Returns error for invalid currency codes
- Case-insensitive input (converts to uppercase)

### Expense Processing Flow

1. **User Creates Account**
   - Specifies preferred currency (optional, defaults to USD)
   - Currency validated against supported list

2. **Invoice Processing**
   - System retrieves user's default currency from database
   - Document analyzed using Azure Document Intelligence
   - Currency extraction attempted from:
     - InvoiceTotal field
     - CurrencyCode field
     - Currency symbols in text ($, €, £, etc.)
     - ISO codes in document content

3. **Fallback Logic**
   ```
   Document Currency → User Default Currency → "USD"
   ```

4. **Webhook Response**
   - Includes currency information at invoice and item levels
   - Currency populated even if not extracted from document

### Supported Currency Codes
```
USD, EUR, GBP, JPY, AUD, CAD, CHF, CNY, SEK, NZD,
MXN, SGD, HKD, NOK, TRY, ZAR, BRL, INR, KRW, PLN,
DKK, CZK, HUF, ILS, CLP, PHP, AED, COP, SAR, MYR,
RON, THB, BGN, HRK, RUB, ISK, IDR, UAH
```

### Currency Symbol Mappings
The system recognizes common currency symbols:
```
$ → USD    € → EUR    £ → GBP    ¥ → JPY
₹ → INR    ₽ → RUB    ¢ → USD    ₦ → NGN
₡ → CRC    ₨ → PKR    ₩ → KRW
```

## Example Usage

### 1. Create User with EUR Currency
```bash
curl -X POST "http://localhost:5256/api/userauthentication/sign-up" \
  -H "Content-Type: application/json" \
  -d '{
    "Username": "european_user",
    "Password": "Password123!",
    "Email": "user@example.eu",
    "DisplayName": "European User",
    "DefaultCurrency": "EUR"
  }'
```

### 2. Webhook Response with Currency
```json
{
  "JobId": "12345-guid",
  "Status": "completed",
  "InvoiceData": {
    "VendorName": "Acme Corp Ltd",
    "InvoiceTotal": 1250.50,
    "Currency": "EUR",
    "InvoiceDate": "2024-01-15",
    "Items": [
      {
        "Description": "Software License",
        "Amount": 1000.00,
        "Currency": "EUR",
        "Quantity": 1
      },
      {
        "Description": "Support (USD charged)",
        "Amount": 250.50,
        "Currency": "USD",
        "Quantity": 1
      }
    ]
  }
}
```

## Error Handling

### Invalid Currency Code
```json
{
  "Success": false,
  "Message": "Invalid currency code. Please use a valid ISO 4217 currency code (e.g., USD, EUR, GBP)."
}
```

### Currency Extraction Failure
- System logs extraction attempts
- Falls back to user's default currency
- Continues processing without failing

## Testing

Use the provided `test_currency.ps1` script to test:
1. User creation with valid currencies
2. User creation with invalid currencies
3. Save-expense API with currency fallback
4. Webhook callbacks with currency data

## Future Enhancements

1. **Exchange Rate Integration**
   - Convert amounts to user's preferred currency
   - Historical exchange rates for accurate reporting

2. **Currency Validation Enhancement**
   - Real-time currency validation against external APIs
   - Support for cryptocurrency codes

3. **Multi-Currency Reporting**
   - Currency-specific expense reports
   - Cross-currency analytics

4. **Regional Settings**
   - Automatic currency detection based on user location
   - Regional number and date formats