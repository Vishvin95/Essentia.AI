namespace secondwifeapi.Models
{
    public class SaveExpenseRequest
    {
        public int GroupId { get; set; }
        public int UserId { get; set; }
        public string BlobSasUrl { get; set; } = string.Empty;
        public string? WebhookUrl { get; set; }
    }
}