# Test script for Analytics APIs
Write-Host "`n=== Testing Analytics APIs ===" -ForegroundColor Cyan

# Base URL - adjust if needed
$baseUrl = "http://localhost:5256/api/analytics"

Write-Host "`n1. Testing User Expense Summary API" -ForegroundColor Yellow
Write-Host "Endpoint: GET $baseUrl/user-summary/{userId}" -ForegroundColor Gray

# Test with user ID 1
$userId = 1
try {
    Write-Host "`nTesting with UserId: $userId" -ForegroundColor White
    $response = Invoke-RestMethod -Uri "$baseUrl/user-summary/$userId" -Method GET
    
    Write-Host "SUCCESS: User Summary retrieved!" -ForegroundColor Green
    Write-Host "User ID: $($response.UserId)" -ForegroundColor Cyan
    Write-Host "Username: $($response.Username)" -ForegroundColor Cyan
    Write-Host "Total Expense Amount: $($response.TotalExpenseAmount) $($response.Currency)" -ForegroundColor Yellow
    Write-Host "Group Count: $($response.GroupCount)" -ForegroundColor Cyan
    Write-Host "Total Expense Count: $($response.TotalExpenseCount)" -ForegroundColor Cyan
    Write-Host "Last Expense Date: $($response.LastExpenseDate)" -ForegroundColor Cyan
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
}

Write-Host "`n2. Testing Group Expense Details API" -ForegroundColor Yellow
Write-Host "Endpoint: GET $baseUrl/group-details/{groupId}" -ForegroundColor Gray

# Test with group ID 1
$groupId = 1
try {
    Write-Host "`nTesting with GroupId: $groupId" -ForegroundColor White
    $response = Invoke-RestMethod -Uri "$baseUrl/group-details/$groupId" -Method GET
    
    Write-Host "SUCCESS: Group Details retrieved!" -ForegroundColor Green
    Write-Host "Group ID: $($response.GroupId)" -ForegroundColor Cyan
    Write-Host "Group Name: $($response.GroupName)" -ForegroundColor Cyan
    Write-Host "Total Group Expenses: $($response.TotalGroupExpenses) $($response.Currency)" -ForegroundColor Yellow
    Write-Host "Total Expense Count: $($response.TotalExpenseCount)" -ForegroundColor Cyan
    
    Write-Host "`nUser Expenses in Group:" -ForegroundColor Magenta
    foreach ($user in $response.UserExpenses) {
        Write-Host "  - $($user.Username) ($($user.DisplayName)): $($user.TotalAmount) $($user.Currency) ($($user.ExpenseCount) expenses)" -ForegroundColor White
    }
    
    Write-Host "`nRecent Expenses (showing first 3):" -ForegroundColor Magenta
    $count = 0
    foreach ($expense in $response.RecentExpenses) {
        if ($count -ge 3) { break }
        Write-Host "  - ExpenseId: $($expense.ExpenseId) | User: $($expense.Username) | Amount: $($expense.TotalAmount) $($expense.Currency) | Vendor: $($expense.VendorName)" -ForegroundColor White
        $count++
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
}

Write-Host "`n3. Testing User Expenses in Group API" -ForegroundColor Yellow
Write-Host "Endpoint: GET $baseUrl/group/{groupId}/user/{userId}" -ForegroundColor Gray

# Test with group ID 1 and user ID 1
try {
    Write-Host "`nTesting with GroupId: $groupId and UserId: $userId" -ForegroundColor White
    $response = Invoke-RestMethod -Uri "$baseUrl/group/$groupId/user/$userId" -Method GET
    
    Write-Host "SUCCESS: User Expenses in Group retrieved!" -ForegroundColor Green
    Write-Host "Found $($response.Count) expenses for user $userId in group $groupId" -ForegroundColor Yellow
    
    if ($response.Count -gt 0) {
        Write-Host "`nExpense Details:" -ForegroundColor Magenta
        foreach ($expense in $response) {
            Write-Host "  - ExpenseId: $($expense.ExpenseId) | Date: $($expense.ExpenseDate) | Amount: $($expense.TotalAmount) $($expense.Currency) | Vendor: $($expense.VendorName)" -ForegroundColor White
            if ($expense.Items.Count -gt 0) {
                Write-Host "    Items:" -ForegroundColor Gray
                foreach ($item in $expense.Items) {
                    Write-Host "      * $($item.Description): $($item.Amount) $($item.Currency) $(if($item.Quantity){"(Qty: $($item.Quantity))"})" -ForegroundColor DarkGray
                }
            }
        }
    } else {
        Write-Host "No expenses found for this user in this group." -ForegroundColor Yellow
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
}

Write-Host "`n4. Testing with invalid data" -ForegroundColor Yellow

# Test with invalid user ID
try {
    Write-Host "`nTesting with invalid UserId: 999" -ForegroundColor White
    $response = Invoke-RestMethod -Uri "$baseUrl/user-summary/999" -Method GET
    Write-Host "Unexpected success with invalid user ID" -ForegroundColor Red
} catch {
    Write-Host "Expected error for invalid user ID: $($_.Exception.Message)" -ForegroundColor Green
}

# Test with invalid group ID
try {
    Write-Host "`nTesting with invalid GroupId: 999" -ForegroundColor White
    $response = Invoke-RestMethod -Uri "$baseUrl/group-details/999" -Method GET
    Write-Host "Unexpected success with invalid group ID" -ForegroundColor Red
} catch {
    Write-Host "Expected error for invalid group ID: $($_.Exception.Message)" -ForegroundColor Green
}

Write-Host "`n=== Analytics API Testing Complete ===" -ForegroundColor Cyan