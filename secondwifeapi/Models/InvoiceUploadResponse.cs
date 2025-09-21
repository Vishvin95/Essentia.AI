namespace secondwifeapi.Models
{
    public class InvoiceUploadResponse
    {
        public string UploadId { get; set; } = string.Empty;
        public string SasUrl { get; set; } = string.Empty;
        public long ExpiresAt { get; set; }
    }
}
