using System.ComponentModel.DataAnnotations;

namespace secondwifeapi.Models
{
    public class VoiceExpenseRequest
    {
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public int GroupId { get; set; }
        
        [Required]
        public string SpeechText { get; set; } = string.Empty;
    }

    public class VoiceExpenseResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ExpenseId { get; set; }
        public ExtractedExpenseData? ExtractedData { get; set; }
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }

    public class ExtractedExpenseData
    {
        public int Quantity { get; set; }
        public double Amount { get; set; }
        public string Item { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string? Merchant { get; set; }
    }

    // Azure OpenAI API models
    public class OpenAIMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class OpenAIFunction
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public object Parameters { get; set; } = new object();
    }

    public class OpenAIRequest
    {
        public List<OpenAIMessage> Messages { get; set; } = new List<OpenAIMessage>();
        public List<OpenAIFunction> Functions { get; set; } = new List<OpenAIFunction>();
        public string Function_call { get; set; } = "auto";
    }

    public class FunctionCall
    {
        public string Name { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
    }

    public class OpenAIChoice
    {
        public OpenAIMessage Message { get; set; } = new OpenAIMessage();
        public FunctionCall? Function_call { get; set; }
    }

    public class OpenAIResponse
    {
        public List<OpenAIChoice> Choices { get; set; } = new List<OpenAIChoice>();
    }
}