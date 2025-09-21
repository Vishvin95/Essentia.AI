# Analytics APIs Documentation

This document describes the Analytics APIs that provide expense summary and detailed views for users and groups.

## Base URL
```
http://localhost:5256/api/analytics
```

## Endpoints

### 1. User Expense Summary
**GET** `/user-summary/{userId}`

Retrieves a comprehensive summary of expenses for a specific user.

#### Parameters
- `userId` (integer) - The ID of the user to get the summary for

#### Response
```json
{
  "userId": 1,
  "username": "vvin",
  "totalExpenseAmount": 1674.00,
  "currency": "USD",
  "groupCount": 2,
  "totalExpenseCount": 9,
  "lastExpenseDate": "2025-09-20T00:00:00"
}
```

#### Fields Description
- `userId`: The user's ID
- `username`: The user's username
- `totalExpenseAmount`: Sum of all expenses for this user across all groups
- `currency`: User's default currency
- `groupCount`: Number of groups the user is a member of
- `totalExpenseCount`: Total number of expenses recorded by this user
- `lastExpenseDate`: Date of the most recent expense

#### Example Usage
```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5256/api/analytics/user-summary/1" -Method GET
```

---

### 2. Group Expense Details
**GET** `/group-details/{groupId}?limit={limit}`

Retrieves detailed expense information for a specific group, including individual user breakdowns and recent expenses.

#### Parameters
- `groupId` (integer) - The ID of the group to get details for
- `limit` (integer, optional) - Maximum number of recent expenses to return (default: 20)

#### Response
```json
{
  "groupId": 1,
  "groupName": "Family",
  "totalGroupExpenses": 1672.00,
  "currency": "USD",
  "totalExpenseCount": 8,
  "userExpenses": [
    {
      "userId": 1,
      "username": "vvin",
      "displayName": "vvin",
      "totalAmount": 1672.00,
      "currency": "USD",
      "expenseCount": 8,
      "lastExpenseDate": "2025-09-20T00:00:00"
    }
  ],
  "recentExpenses": [
    {
      "expenseId": 8,
      "userId": 1,
      "username": "vvin",
      "displayName": "vvin",
      "expenseDate": "2025-09-20T00:00:00",
      "vendorName": "Voice Entry",
      "totalAmount": 2.00,
      "currency": "USD",
      "items": [
        {
          "expenseItemId": 11,
          "description": "milk (Voice Entry)",
          "amount": 2.00,
          "currency": "USD",
          "quantity": 1
        }
      ]
    }
  ]
}
```

#### Fields Description
- `groupId`: The group's ID
- `groupName`: The group's name
- `totalGroupExpenses`: Sum of all expenses in this group
- `currency`: Default currency for the group
- `totalExpenseCount`: Total number of expenses in the group
- `userExpenses`: Array of user expense summaries within the group
- `recentExpenses`: Array of recent expense details with items

#### Example Usage
```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5256/api/analytics/group-details/1?limit=10" -Method GET
```

---

### 3. User Expenses in Group
**GET** `/group/{groupId}/user/{userId}`

Retrieves all expenses for a specific user within a specific group.

#### Parameters
- `groupId` (integer) - The ID of the group
- `userId` (integer) - The ID of the user

#### Response
```json
[
  {
    "expenseId": 8,
    "userId": 1,
    "username": "vvin",
    "displayName": "vvin",
    "expenseDate": "2025-09-20T00:00:00",
    "vendorName": "Voice Entry",
    "totalAmount": 2.00,
    "currency": "USD",
    "items": [
      {
        "expenseItemId": 11,
        "description": "milk (Voice Entry)",
        "amount": 2.00,
        "currency": "USD",
        "quantity": 1
      }
    ]
  }
]
```

#### Example Usage
```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5256/api/analytics/group/1/user/1" -Method GET
```

---

### 4. Group Summary
**GET** `/group-summary/{groupId}`

Retrieves comprehensive summary statistics for a specific group including financial metrics, member information, and activity analysis.

#### Parameters
- `groupId` (integer) - The ID of the group to get summary for

#### Response
```json
{
  "groupId": 1,
  "groupName": "Family",
  "description": "Family expenses",
  "adminUserId": 1,
  "adminUsername": "vvin",
  "adminDisplayName": "vvin",
  "totalExpenses": 1672.00,
  "currency": "USD",
  "totalExpenseCount": 8,
  "memberCount": 1,
  "createdAt": "2025-09-18T14:04:36.473",
  "lastExpenseDate": "2025-09-20T00:00:00",
  "averageExpenseAmount": 209.00,
  "mostFrequentVendor": "Voice Entry",
  "vendorCount": 3,
  "expenseFrequency": {
    "expensesLast7Days": 2,
    "expensesLast30Days": 8,
    "averageExpensesPerWeek": 1.5,
    "averageExpensesPerMonth": 6.2
  },
  "members": [
    {
      "userId": 1,
      "username": "vvin",
      "displayName": "vvin",
      "joinedAt": "2025-09-18T14:04:36.473",
      "isAdmin": true
    }
  ]
}
```

#### Fields Description
**Basic Group Information:**
- `groupId`: The group's ID
- `groupName`: The group's name
- `description`: Group description
- `adminUserId`, `adminUsername`, `adminDisplayName`: Admin user information
- `createdAt`: When the group was created
- `memberCount`: Number of active members

**Financial Metrics:**
- `totalExpenses`: Sum of all expenses in this group
- `currency`: Group's default currency
- `totalExpenseCount`: Total number of expenses
- `averageExpenseAmount`: Average amount per expense
- `lastExpenseDate`: Date of most recent expense

**Vendor Analysis:**
- `mostFrequentVendor`: Vendor name that appears most often
- `vendorCount`: Number of unique vendors

**Activity Analysis:**
- `expenseFrequency.expensesLast7Days`: Number of expenses in last 7 days
- `expenseFrequency.expensesLast30Days`: Number of expenses in last 30 days
- `expenseFrequency.averageExpensesPerWeek`: Historical average expenses per week
- `expenseFrequency.averageExpensesPerMonth`: Historical average expenses per month

**Member Information:**
- `members`: Array of group members with join dates and admin status (no expense data)

#### Example Usage
```powershell
# Get summary for group 1
$response = Invoke-RestMethod -Uri "http://localhost:5256/api/analytics/group-summary/1" -Method GET

# Get summary for group 2
$response = Invoke-RestMethod -Uri "http://localhost:5256/api/analytics/group-summary/2" -Method GET
```

---

## Error Responses

All endpoints return appropriate HTTP status codes:

- `200 OK` - Success
- `404 Not Found` - User, group, or user-group membership not found
- `500 Internal Server Error` - Server error

### Error Response Format
```json
{
  "error": "User with ID 999 not found"
}
```

---

## Use Cases

### 1. User Dashboard
Use the User Expense Summary endpoint to display:
- Total spending across all groups
- Number of groups user participates in
- Overall expense activity

### 2. Group Management
Use the Group Expense Details endpoint to:
- View total group spending
- See which users spend the most
- Review recent group activities
- Analyze expense patterns

### 3. Personal Group Analysis
Use the User Expenses in Group endpoint to:
- Show individual contributions to specific groups
- Track personal spending within a group context
- Generate personal reports for specific groups

### 4. Group-Level Analysis
Use the Group Summary endpoint to:
- Analyze individual group performance and health
- Monitor group activity patterns and trends
- Track vendor diversity and spending patterns
- Understand member participation (join dates, admin status)
- Generate detailed group reports for administrators
- Compare groups by fetching multiple group summaries

---

## Testing

Run the test scripts to verify all endpoints:

### Test all original analytics endpoints:
```powershell
.\test_analytics.ps1
```

### Test the new group summary endpoint:
```powershell
.\test_group_summary.ps1
```

These scripts will test all four endpoints with sample data and error scenarios.