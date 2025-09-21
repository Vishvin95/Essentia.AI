using System.ComponentModel.DataAnnotations;

namespace secondwifeapi.Models
{
    public class ExpenseItem
    {
        [Key]
        public int ExpenseItemId { get; set; }
        
        // Foreign key to Expense
        public int ExpenseId { get; set; }
        
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public double? Quantity { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Navigation property to parent expense
        public virtual Expense Expense { get; set; } = null!;
    }
}