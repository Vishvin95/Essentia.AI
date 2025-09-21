namespace secondwifeapi.Models
{
    public class InvoiceUploadRequest
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string? Checksum { get; set; }
    }
}
