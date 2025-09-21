namespace secondwifeapi.Models
{
    public class ManualExpenseRequest
    {
        public int UserId { get; set; }
        public int GroupId { get; set; }
        public string VendorName { get; set; } = string.Empty; // merchant/vendor/place
        public string ItemName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Currency { get; set; } // Optional, defaults to USD
        public DateTime? Date { get; set; } // Optional, defaults to today
    }

    public class ManualExpenseResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ExpenseId { get; set; } // Primary key of the expense
        public decimal TotalAmount { get; set; } // Total amount for the expense (may include other items)
        public string Currency { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }
}