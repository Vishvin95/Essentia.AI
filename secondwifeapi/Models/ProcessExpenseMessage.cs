namespace secondwifeapi.Models
{
    public class ProcessExpenseMessage
    {
        public string JobId { get; set; } = string.Empty;
        public int GroupId { get; set; }
        public int UserId { get; set; }
        public string BlobSasUrl { get; set; } = string.Empty;
        public string? WebhookUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}