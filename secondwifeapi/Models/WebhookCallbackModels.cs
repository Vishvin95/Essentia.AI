namespace secondwifeapi.Models
{
    public class WebhookCallbackRequest
    {
        public string JobId { get; set; } = string.Empty;
        public int GroupId { get; set; }
        public int UserId { get; set; }
        public string BlobSasUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "completed" or "failed"
        public DateTime ProcessedAt { get; set; }
        public InvoiceDataResult? InvoiceData { get; set; }
        public string? Error { get; set; }
    }

    public class InvoiceDataResult
    {
        public string? VendorName { get; set; }
        public decimal? InvoiceTotal { get; set; }
        public string? Currency { get; set; } // ISO 4217 currency code
        public DateTime? InvoiceDate { get; set; }
        public List<InvoiceItem>? Items { get; set; }
    }

    public class InvoiceItem
    {
        public string? Description { get; set; }
        public decimal? Amount { get; set; }
        public string? Currency { get; set; } // ISO 4217 currency code, can be different per item
        public double? Quantity { get; set; }
    }

    public class WebhookCallbackResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? JobId { get; set; }
    }
}