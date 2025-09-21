using Azure.AI.OpenAI;
using Newtonsoft.Json;
using secondwifeapi.Models;

namespace secondwifeapi.Services
{
    public interface IContextExtractionService
    {
        Task<OpenAIContextResponse> ExtractStructuredContextAsync(OpenAIContextRequest request);
    }

    public class ContextExtractionService : IContextExtractionService
    {
        private readonly OpenAIClient _openAIClient;
        private readonly string _deploymentName;
        private readonly ILogger<ContextExtractionService> _logger;

        public ContextExtractionService(OpenAIClient openAIClient, IConfiguration configuration, ILogger<ContextExtractionService> logger)
        {
            _openAIClient = openAIClient;
            _deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? throw new ArgumentNullException("AzureOpenAI:DeploymentName");
            _logger = logger;
        }

        public async Task<OpenAIContextResponse> ExtractStructuredContextAsync(OpenAIContextRequest request)
        {
            try
            {
                _logger.LogInformation($"Extracting structured context for user {request.UserId}");

                var systemPrompt = $@"
You are an AI assistant that extracts structured information from user context text. 
Your task is to analyze the user's input and extract structured data including type, dates, locations, people, and other relevant information.

Given the user's context text, extract the following information and return it as JSON:
1. type: The type of context (event, travel, personal, work, health, social, financial, etc.)
2. confidence: A confidence score between 0.0 and 1.0 for the extraction accuracy
3. extractedData: A dictionary with key-value pairs of extracted information specific to the context type
4. tags: An array of relevant tags or keywords
5. dateReferences: An array of date references found in the text with their parsed dates
6. locationReferences: An array of location names mentioned
7. peopleReferences: An array of people mentioned

For date references, include:
- originalText: The original date text from the user
- parsedDate: The parsed date in ISO format (YYYY-MM-DD) for single dates only (only if exact date can be determined)
- startDate: Start date in ISO format (YYYY-MM-DD) for date ranges (only if exact dates can be determined)
- endDate: End date in ISO format (YYYY-MM-DD) for date ranges (only if exact dates can be determined)
- dateType: 'specific' (exact date/time), 'relative' (this week, next Monday), 'range' (exact date range), 'fuzzy' (weekend, soon), 'recurring' (every Monday)
- relativeDescription: The relative time description (e.g., ""next weekend"", ""this Friday"", ""every Monday"")
- confidence: Confidence in the date parsing
- requiresContext: true if additional context is needed to determine exact dates

DATE CALCULATION GUIDELINES:
- ""Monday""/""Tuesday""/etc: Calculate next occurrence of that weekday
- ""next Monday"": Next Monday from today  
- ""this Monday"": Monday of current week (if already passed, next Monday)
- ""next week"": Monday to Sunday of next week (from Sept 29 to Oct 5, 2025)
- ""this week"": Monday to Sunday of current week (from Sept 22 to Sept 28, 2025)
- ""tomorrow"": Next day (Sept 22, 2025)
- ""today"": Current date (Sept 21, 2025)
- ""weekend"": Fuzzy - don't calculate specific dates

IMPORTANT RULES:
- Only set parsedDate/startDate/endDate if you can determine the EXACT date with high confidence
- For specific day names (""Monday"", ""Tuesday""), assume the NEXT occurrence of that day and set exact date
- For ""next week"", calculate the specific Monday-Sunday range and use startDate/endDate
- For ""this week"", use current week's Monday-Sunday range
- For vague references like ""weekend"", ""soon"", ""later"" without clear context, use dateType: ""fuzzy""
- Always include relativeDescription with the original time reference
- Set requiresContext: true when the date reference is ambiguous

Current date for reference: {request.CurrentDate:yyyy-MM-dd} (Today is {request.CurrentDate:dddd})

Example outputs:

1. Specific day name (""Meeting on Monday""):
{{
  ""type"": ""meeting"",
  ""confidence"": 0.9,
  ""extractedData"": {{""event"": ""meeting""}},
  ""tags"": [""meeting"", ""work""],
  ""dateReferences"": [
    {{
      ""originalText"": ""Monday"",
      ""parsedDate"": ""2025-09-22"",
      ""dateType"": ""specific"",
      ""relativeDescription"": ""next Monday"",
      ""confidence"": 0.9,
      ""requiresContext"": false
    }}
  ],
  ""locationReferences"": [],
  ""peopleReferences"": []
}}

2. Next week reference (""I'll be traveling next week""):
{{
  ""type"": ""travel"",
  ""confidence"": 0.9,
  ""extractedData"": {{""purpose"": ""travel""}},
  ""tags"": [""travel"", ""work""],
  ""dateReferences"": [
    {{
      ""originalText"": ""next week"",
      ""startDate"": ""2025-09-29"",
      ""endDate"": ""2025-10-05"",
      ""dateType"": ""range"",
      ""relativeDescription"": ""next week"",
      ""confidence"": 0.9,
      ""requiresContext"": false
    }}
  ],
  ""locationReferences"": [],
  ""peopleReferences"": []
}}

3. This week reference (""Working from home this week""):
{{
  ""type"": ""work_arrangement"",
  ""confidence"": 0.9,
  ""extractedData"": {{""arrangement"": ""work from home""}},
  ""tags"": [""work"", ""remote""],
  ""dateReferences"": [
    {{
      ""originalText"": ""this week"",
      ""startDate"": ""2025-09-22"",
      ""endDate"": ""2025-09-28"",
      ""dateType"": ""range"",
      ""relativeDescription"": ""this week"",
      ""confidence"": 0.9,
      ""requiresContext"": false
    }}
  ],
  ""locationReferences"": [],
  ""peopleReferences"": []
}}

4. Fuzzy date reference (""Party on weekend""):
{{
  ""type"": ""social"",
  ""confidence"": 0.8,
  ""extractedData"": {{""event"": ""party""}},
  ""tags"": [""party"", ""social"", ""weekend""],
  ""dateReferences"": [
    {{
      ""originalText"": ""weekend"",
      ""dateType"": ""fuzzy"",
      ""relativeDescription"": ""weekend"",
      ""confidence"": 0.6,
      ""requiresContext"": true
    }}
  ],
  ""locationReferences"": [],
  ""peopleReferences"": []
}}

5. Tomorrow reference (""Call client tomorrow""):
{{
  ""type"": ""task"",
  ""confidence"": 0.95,
  ""extractedData"": {{""task"": ""call client""}},
  ""tags"": [""call"", ""client"", ""work""],
  ""dateReferences"": [
    {{
      ""originalText"": ""tomorrow"",
      ""parsedDate"": ""2025-09-22"",
      ""dateType"": ""specific"",
      ""relativeDescription"": ""tomorrow"",
      ""confidence"": 0.95,
      ""requiresContext"": false
    }}
  ],
  ""locationReferences"": [],
  ""peopleReferences"": []
}}

Be concise and accurate. If you're unsure about something, use a lower confidence score.
";

                var userPrompt = $"User context text: \"{request.UserText}\"";

                var chatCompletionsOptions = new ChatCompletionsOptions()
                {
                    DeploymentName = _deploymentName,
                    Messages =
                    {
                        new ChatRequestSystemMessage(systemPrompt),
                        new ChatRequestUserMessage(userPrompt)
                    },
                    Temperature = 0.1f,
                    MaxTokens = 1000,
                };

                var response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
                var responseContent = response.Value.Choices[0].Message.Content;

                _logger.LogInformation($"Azure OpenAI response received for user {request.UserId}");

                // Parse the JSON response
                var structuredContext = JsonConvert.DeserializeObject<StructuredContext>(responseContent);
                
                if (structuredContext == null)
                {
                    _logger.LogWarning($"Failed to parse OpenAI response for user {request.UserId}");
                    return new OpenAIContextResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to parse AI response"
                    };
                }

                return new OpenAIContextResponse
                {
                    StructuredContext = structuredContext,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting structured context for user {request.UserId}");
                return new OpenAIContextResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}