namespace secondwifeapi.Models
{
    // Response model for user expense summary
    public class UserExpenseSummaryResponse
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public decimal TotalExpenseAmount { get; set; }
        public string Currency { get; set; } = "USD";
        public int GroupCount { get; set; }
        public int TotalExpenseCount { get; set; }
        public DateTime LastExpenseDate { get; set; }
    }

    // Response model for group expense details
    public class GroupExpenseDetailsResponse
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public decimal TotalGroupExpenses { get; set; }
        public string Currency { get; set; } = "USD";
        public int TotalExpenseCount { get; set; }
        public List<UserExpenseInGroup> UserExpenses { get; set; } = new List<UserExpenseInGroup>();
        public List<ExpenseDetail> RecentExpenses { get; set; } = new List<ExpenseDetail>();
    }

    // Individual user expenses within a group
    public class UserExpenseInGroup
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "USD";
        public int ExpenseCount { get; set; }
        public DateTime? LastExpenseDate { get; set; }
    }

    // Detailed expense information
    public class ExpenseDetail
    {
        public int ExpenseId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string? VendorName { get; set; }
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "USD";
        public List<ExpenseItemDetail> Items { get; set; } = new List<ExpenseItemDetail>();
    }

    // Individual expense item details
    public class ExpenseItemDetail
    {
        public int ExpenseItemId { get; set; }
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public double? Quantity { get; set; }
    }

    // Response model for specific group-level summary
    public class GroupSummaryResponse
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int AdminUserId { get; set; }
        public string AdminUsername { get; set; } = string.Empty;
        public string? AdminDisplayName { get; set; }
        public decimal TotalExpenses { get; set; }
        public string Currency { get; set; } = "USD";
        public int TotalExpenseCount { get; set; }
        public int MemberCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastExpenseDate { get; set; }
        public decimal AverageExpenseAmount { get; set; }
        public string? MostFrequentVendor { get; set; }
        public int VendorCount { get; set; }
        public ExpenseFrequency ExpenseFrequency { get; set; } = new ExpenseFrequency();
        public List<GroupMemberSummary> Members { get; set; } = new List<GroupMemberSummary>();
    }

    // Group member information (not expense breakdown)
    public class GroupMemberSummary
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public DateTime JoinedAt { get; set; }
        public bool IsAdmin { get; set; }
    }

    // Expense frequency analysis
    public class ExpenseFrequency
    {
        public int ExpensesLast7Days { get; set; }
        public int ExpensesLast30Days { get; set; }
        public decimal AverageExpensesPerWeek { get; set; }
        public decimal AverageExpensesPerMonth { get; set; }
    }
}