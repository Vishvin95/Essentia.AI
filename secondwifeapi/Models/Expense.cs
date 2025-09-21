using System.ComponentModel.DataAnnotations;

namespace secondwifeapi.Models
{
    public class Expense
    {
        [Key]
        public int ExpenseId { get; set; }
        
        public int UserId { get; set; }
        public int GroupId { get; set; }
        public DateTime ExpenseDate { get; set; } // Populated from invoice date for captured expenses, provided by user for manual expenses
        public string? VendorName { get; set; }
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "USD";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Group Group { get; set; } = null!;
        public virtual ICollection<ExpenseItem> ExpenseItems { get; set; } = new List<ExpenseItem>();
    }
}