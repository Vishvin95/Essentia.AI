# Quantity vs Amount Extraction Fix - Summary

## Problem Identified
The Azure OpenAI API was sometimes extracting **quantity** values instead of **monetary amounts** when processing voice expense text. For example, "I bought 3 coffees for 15 dollars" might extract amount=3 instead of amount=15.

## Root Cause
- The original function definition was not explicit enough about distinguishing between quantity and cost
- No system message to guide the AI model
- Ambiguous parameter descriptions

## Solutions Implemented

### 1. Enhanced Function Description
**Before:**
```
Description = "Extract amount, currency, item, merchant/place, and optional date from a free-form expense sentence"
```

**After:**
```
Description = "Extract the total monetary amount, currency, item description, merchant/place, and optional date from a free-form expense sentence. Focus on the TOTAL COST, not quantity."
```

### 2. Improved Parameter Definition
**Before:**
```
amount = new { type = "number", description = "The numerical amount without currency symbol" }
```

**After:**
```
amount = new { type = "number", description = "The total monetary amount spent (total cost, not quantity). Example: if someone says 'I bought 3 coffees for 15 dollars', the amount should be 15, not 3." }
```

### 3. Added System Message
Added a system message to provide better context:
```
"You are an expense extraction assistant. When users describe purchases, extract the TOTAL MONETARY AMOUNT they spent, not the quantity of items. For example: 'I bought 3 coffees for 12 dollars' means amount=12, not amount=3. Always focus on the money spent, not the number of items."
```

### 4. Added Validation Warning
Added logic to detect potential quantity/amount confusion:
```csharp
// Validation: Check if amount seems suspiciously low (might be quantity instead of cost)
if (amount <= 10 && rawCurrency.ToUpper() == "USD" && speechText.Contains("dollars"))
{
    _logger.LogWarning("Potential quantity/amount confusion detected. Amount: {Amount}, Speech: {Speech}", amount, speechText);
}
```

## Test Cases to Validate Fix

| Test Scenario | Speech Text | Expected Amount | Common Error |
|---------------|-------------|----------------|--------------|
| Multiple items | "I bought 3 coffees for 15 dollars" | 15 | Previously: 3 |
| Quantity first | "I got 2 sandwiches and paid 18 dollars" | 18 | Previously: 2 |
| Per-item pricing | "4 drinks at 3 dollars each for 12 dollars total" | 12 | Previously: 4 or 3 |
| Simple cost | "I paid 25 dollars for groceries" | 25 | ✓ Usually correct |

## Files Modified
- `Services/ExpenseExtractionService.cs` - Enhanced function definition and added system message
- `test_quantity_amount.ps1` - Created comprehensive test suite

## Expected Impact
- More accurate amount extraction from voice input
- Reduced confusion between quantity and monetary values
- Better logging to identify potential issues
- Improved user experience with voice expense entry

## Testing
Run `.\test_quantity_amount.ps1` to validate the fix works correctly with various speech patterns that commonly cause confusion.