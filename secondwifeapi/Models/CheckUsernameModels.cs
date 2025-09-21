namespace secondwifeapi.Models
{
    public class CheckUsernameRequest
    {
        public string Username { get; set; } = string.Empty;
    }

    public class CheckUsernameResponse
    {
        public bool Exists { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}