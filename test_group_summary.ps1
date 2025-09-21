# Test script for Group Summary Analytics API
Write-Host "`n=== Testing Group Summary Analytics API ===" -ForegroundColor Cyan

# Base URL - adjust if needed
$baseUrl = "http://localhost:5256/api/analytics"

Write-Host "`n1. Testing Group Summary API for Group 1" -ForegroundColor Yellow
Write-Host "Endpoint: GET $baseUrl/group-summary/1" -ForegroundColor Gray

try {
    Write-Host "`nTesting group summary for Group ID 1..." -ForegroundColor White
    $response = Invoke-RestMethod -Uri "$baseUrl/group-summary/1" -Method GET
    
    Write-Host "SUCCESS: Group Summary retrieved!" -ForegroundColor Green
    Write-Host "Group ID: $($response.GroupId)" -ForegroundColor Cyan
    Write-Host "Group Name: $($response.GroupName)" -ForegroundColor Cyan
    Write-Host "Description: $($response.Description)" -ForegroundColor Gray
    Write-Host "Admin: $($response.AdminUsername) ($($response.AdminDisplayName))" -ForegroundColor Yellow
    Write-Host "Total Expenses: $($response.TotalExpenses) $($response.Currency)" -ForegroundColor Yellow
    Write-Host "Total Expense Count: $($response.TotalExpenseCount)" -ForegroundColor Cyan
    Write-Host "Member Count: $($response.MemberCount)" -ForegroundColor Cyan
    Write-Host "Average Expense Amount: $($response.AverageExpenseAmount) $($response.Currency)" -ForegroundColor Yellow
    Write-Host "Most Frequent Vendor: $($response.MostFrequentVendor)" -ForegroundColor Magenta
    Write-Host "Vendor Count: $($response.VendorCount)" -ForegroundColor Cyan
    Write-Host "Created At: $($response.CreatedAt)" -ForegroundColor Gray
    if ($response.LastExpenseDate) {
        Write-Host "Last Expense Date: $($response.LastExpenseDate)" -ForegroundColor Gray
    }
    
    Write-Host "`nExpense Frequency Analysis:" -ForegroundColor Magenta
    Write-Host "  - Expenses Last 7 Days: $($response.ExpenseFrequency.ExpensesLast7Days)" -ForegroundColor White
    Write-Host "  - Expenses Last 30 Days: $($response.ExpenseFrequency.ExpensesLast30Days)" -ForegroundColor White
    Write-Host "  - Average Expenses Per Week: $($response.ExpenseFrequency.AverageExpensesPerWeek)" -ForegroundColor White
    Write-Host "  - Average Expenses Per Month: $($response.ExpenseFrequency.AverageExpensesPerMonth)" -ForegroundColor White
    
    Write-Host "`nGroup Members:" -ForegroundColor Magenta
    foreach($member in $response.Members) {
        $adminTag = if ($member.IsAdmin) { " (Admin)" } else { "" }
        Write-Host "  - $($member.Username) ($($member.DisplayName))$adminTag - Joined: $($member.JoinedAt)" -ForegroundColor White
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
}

Write-Host "`n2. Testing Group Summary API for Group 2" -ForegroundColor Yellow
Write-Host "Endpoint: GET $baseUrl/group-summary/2" -ForegroundColor Gray

try {
    Write-Host "`nTesting group summary for Group ID 2..." -ForegroundColor White
    $response = Invoke-RestMethod -Uri "$baseUrl/group-summary/2" -Method GET
    
    Write-Host "SUCCESS: Group 2 Summary retrieved!" -ForegroundColor Green
    Write-Host "Group Name: $($response.GroupName)" -ForegroundColor Cyan
    Write-Host "Total Expenses: $($response.TotalExpenses) $($response.Currency)" -ForegroundColor Yellow
    Write-Host "Member Count: $($response.MemberCount)" -ForegroundColor Cyan
    Write-Host "Average Expense: $($response.AverageExpenseAmount) $($response.Currency)" -ForegroundColor Yellow
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
}

Write-Host "`n3. Testing Group Summary API - Comparison View" -ForegroundColor Yellow

# Test multiple groups for comparison
$groupIds = @(1, 2)
$groupSummaries = @()

foreach ($groupId in $groupIds) {
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/group-summary/$groupId" -Method GET
        $groupSummaries += $response
    } catch {
        Write-Host "Could not get summary for Group $groupId" -ForegroundColor Yellow
    }
}

if ($groupSummaries.Count -gt 1) {
    Write-Host "`nGroup Comparison:" -ForegroundColor Magenta
    foreach ($group in $groupSummaries) {
        Write-Host "  - $($group.GroupName): $($group.TotalExpenses) $($group.Currency) ($($group.TotalExpenseCount) expenses, $($group.MemberCount) members)" -ForegroundColor White
    }
    
    $totalAcrossGroups = ($groupSummaries | Measure-Object -Property TotalExpenses -Sum).Sum
    Write-Host "  Total Across All Groups: $totalAcrossGroups" -ForegroundColor Yellow
}

Write-Host "`n4. Testing with Invalid Group ID" -ForegroundColor Yellow

try {
    Write-Host "`nTesting with invalid GroupId: 999" -ForegroundColor White
    $response = Invoke-RestMethod -Uri "$baseUrl/group-summary/999" -Method GET
    Write-Host "Unexpected success with invalid group ID" -ForegroundColor Red
} catch {
    Write-Host "Expected error for invalid group ID: $($_.Exception.Message)" -ForegroundColor Green
}

Write-Host "`n=== Group Summary API Testing Complete ===" -ForegroundColor Cyan