namespace secondwifeapi.Models
{
    public class SignUpRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public string DefaultCurrency { get; set; } = "USD"; // ISO 4217 currency code
    }

    public class SignUpResponse
    {
        public bool Success { get; set; }
        public string? UserId { get; set; }
        public string? Message { get; set; }
        public string? Token { get; set; }
    }
}