using Newtonsoft.Json;

namespace secondwifeapi.Models
{
    // Request model for user context input
    public class UserContextRequest
    {
        public int UserId { get; set; }
        public string ContextText { get; set; } = string.Empty;
    }

    // Response model for user context processing
    public class UserContextResponse
    {
        public string ContextId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public UserContext SavedContext { get; set; } = new UserContext();
    }

    // Main UserContext model for Cosmos DB storage
    public class UserContext
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty; // Cosmos DB document id

        [JsonProperty("userId")]
        public int UserId { get; set; }

        [JsonProperty("contextId")]
        public string ContextId { get; set; } = string.Empty;

        [JsonProperty("contextText")]
        public string ContextText { get; set; } = string.Empty;

        [JsonProperty("structuredContext")]
        public StructuredContext StructuredContext { get; set; } = new StructuredContext();

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        [JsonProperty("isActive")]
        public bool IsActive { get; set; } = true;
    }

    // Structured context extracted by Azure OpenAI
    public class StructuredContext
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("confidence")]
        public double Confidence { get; set; }

        [JsonProperty("extractedData")]
        public Dictionary<string, object> ExtractedData { get; set; } = new Dictionary<string, object>();

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        [JsonProperty("dateReferences")]
        public List<DateReference> DateReferences { get; set; } = new List<DateReference>();

        [JsonProperty("locationReferences")]
        public List<string> LocationReferences { get; set; } = new List<string>();

        [JsonProperty("peopleReferences")]
        public List<string> PeopleReferences { get; set; } = new List<string>();
    }

    // Date reference extracted from context
    public class DateReference
    {
        [JsonProperty("originalText")]
        public string OriginalText { get; set; } = string.Empty;

        [JsonProperty("parsedDate")]
        public DateTime? ParsedDate { get; set; } // Only for specific, determinable dates

        [JsonProperty("startDate")]
        public DateTime? StartDate { get; set; } // Only for specific date ranges

        [JsonProperty("endDate")]
        public DateTime? EndDate { get; set; } // Only for specific date ranges

        [JsonProperty("dateType")]
        public string DateType { get; set; } = string.Empty; // "specific", "relative", "range", "recurring", "fuzzy"

        [JsonProperty("relativeDescription")]
        public string RelativeDescription { get; set; } = string.Empty; // "next weekend", "this Friday", "every Monday"

        [JsonProperty("confidence")]
        public double Confidence { get; set; }

        [JsonProperty("requiresContext")]
        public bool RequiresContext { get; set; } = false; // true for "weekend", "next week", etc.
    }

    // Azure OpenAI service request/response models
    public class OpenAIContextRequest
    {
        public string UserText { get; set; } = string.Empty;
        public int UserId { get; set; }
        public DateTime CurrentDate { get; set; }
    }

    public class OpenAIContextResponse
    {
        public StructuredContext StructuredContext { get; set; } = new StructuredContext();
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}