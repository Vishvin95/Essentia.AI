using System.Text;
using System.Text.Json;
using secondwifeapi.Models;

namespace secondwifeapi.Services
{
    public interface IExpenseExtractionService
    {
        Task<ExtractedExpenseData?> ExtractExpenseAsync(string speechText);
    }

    public class AzureOpenAIExtractionService : IExpenseExtractionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureOpenAIExtractionService> _logger;

        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly string _deploymentName;

        public AzureOpenAIExtractionService(
            HttpClient httpClient, 
            IConfiguration configuration, 
            ILogger<AzureOpenAIExtractionService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _endpoint = _configuration["AzureOpenAI:Endpoint"] ?? "";
            _apiKey = _configuration["AzureOpenAI:ApiKey"] ?? "";
            _deploymentName = _configuration["AzureOpenAI:DeploymentName"] ?? "";
        }

        public async Task<ExtractedExpenseData?> ExtractExpenseAsync(string speechText)
        {
            try
            {
                if (string.IsNullOrEmpty(_endpoint) || string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_deploymentName))
                {
                    _logger.LogError("Azure OpenAI service not configured properly");
                    return null;
                }

                var requestBody = CreateRequestBody(speechText);
                var url = $"{_endpoint}/openai/deployments/{_deploymentName}/chat/completions?api-version=2024-02-15-preview";

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
                };

                request.Headers.Add("api-key", _apiKey);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Azure OpenAI API call failed: {StatusCode} {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (openAIResponse?.Choices?.Count == 0)
                {
                    _logger.LogError("No choices in API response");
                    return null;
                }

                var choice = openAIResponse!.Choices[0];
                var functionCall = choice.Function_call;

                if (functionCall == null || functionCall.Name != "extract_expense")
                {
                    _logger.LogError("No valid function call found in response");
                    return null;
                }

                var arguments = JsonSerializer.Deserialize<JsonElement>(functionCall.Arguments);
                
                if (!arguments.TryGetProperty("quantity", out var quantityElement) ||
                    !arguments.TryGetProperty("amount", out var amountElement) ||
                    !arguments.TryGetProperty("currency", out var currencyElement) ||
                    !arguments.TryGetProperty("item", out var itemElement))
                {
                    _logger.LogError("Missing required fields in function call arguments");
                    return null;
                }

                var quantity = quantityElement.GetInt32();
                var amount = amountElement.GetDouble();
                var rawCurrency = currencyElement.GetString() ?? "USD";
                var item = itemElement.GetString() ?? "";

                // Validation: Check if amount seems suspiciously low (might be quantity instead of cost)
                if (amount <= 10 && rawCurrency.ToUpper() == "USD" && speechText.Contains("dollars"))
                {
                    _logger.LogWarning("Potential quantity/amount confusion detected. Amount: {Amount}, Speech: {Speech}", amount, speechText);
                }

                arguments.TryGetProperty("merchant", out var merchantElement);
                arguments.TryGetProperty("date", out var dateElement);

                var merchant = merchantElement.ValueKind != JsonValueKind.Null ? merchantElement.GetString() : null;
                var dateString = dateElement.ValueKind != JsonValueKind.Null ? dateElement.GetString() : null;

                var extractedExpense = new ExtractedExpenseData
                {
                    Quantity = quantity,
                    Amount = amount,
                    Item = item,
                    Currency = NormalizeCurrency(rawCurrency),
                    Merchant = merchant,
                    Date = ProcessDate(dateString)
                };

                return extractedExpense;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting expense from speech text: {SpeechText}", speechText);
                return null;
            }
        }

        private OpenAIRequest CreateRequestBody(string speechText)
        {
            return new OpenAIRequest
            {
                Messages = new List<OpenAIMessage>
                {
                    new OpenAIMessage
                    {
                        Role = "system",
                        Content = "You are an expense extraction assistant specialized in extracting specific items and merchant locations from purchase descriptions. " +
                        
                        "CRITICAL RULES FOR NUMBER IDENTIFICATION: " +
                        "1. QUANTITY = Number immediately before item name (2 liters, 3 coffees, 5 apples) " +
                        "2. AMOUNT = Number with currency words (Rs 120, $50, 150 rupees, 25 dollars) " +
                        "3. If you see 'Rs 120' or '120 rupees' - the 120 is AMOUNT, not quantity " +
                        "4. If you see '2 liters milk for Rs 120' - quantity=2, amount=120 " +
                        
                        "SENTENCE ANALYSIS EXAMPLES: " +
                        "✓ 'I got 2 liters milk for Rs 120' → quantity=2, amount=120, currency=INR " +
                        "✓ 'I bought 3 coffees for 12 dollars' → quantity=3, amount=12, currency=USD " +
                        "✓ 'Got 5 apples for 20 rupees total' → quantity=5, amount=20, currency=INR " +
                        "✓ 'Bought cheese for 150 rupees' → quantity=1, amount=150, currency=INR " +
                        "✓ 'Spent 25 dollars on a book' → quantity=1, amount=25, currency=USD " +
                        
                        "MERCHANT IDENTIFICATION PATTERNS: " +
                        "• 'FROM [place]' → merchant=[place] (e.g., 'from D-Mart' → merchant='D-Mart') " +
                        "• 'WENT TO [place]' → merchant=[place] (e.g., 'went to Walmart' → merchant='Walmart') " +
                        "• 'AT [place]' → merchant=[place] (e.g., 'at Starbucks' → merchant='Starbucks') " +
                        "• Look for words after FROM, WENT TO, AT - these indicate the merchant/place " +
                        
                        "CURRENCY DETECTION: " +
                        "• Rs, rupees, ₹ → INR " +
                        "• $, dollars, USD → USD " +
                        "• €, euros, EUR → EUR " +
                        
                        "ITEM IDENTIFICATION: Always identify the SPECIFIC ITEM purchased, never use generic categories. Items can be ANY object people buy, including but not limited to: " +
                        "• Food items: cheese, milk, bread, apples, bananas, eggs, chicken, rice, pasta, yogurt, butter, sugar, salt, tea, coffee, juice, water, snacks, chocolates, pizza, burgers, sandwiches, fruits, vegetables, meat, fish, cereals, and any other food products " +
                        "• Household items: soap, shampoo, toothpaste, toilet paper, detergent, cleaning supplies, batteries, light bulbs, towels, bedsheets, dishes, cookware, furniture, decorations, and similar household products " +
                        "• Clothing: shirt, pants, shoes, socks, underwear, jacket, dress, hat, belt, tie, gloves, scarves, bags, and any other apparel or accessories " +
                        "• Electronics: phone, charger, headphones, laptop, tablet, camera, speaker, cables, TV, gaming console, smartwatch, and similar electronic devices " +
                        "• Transport: gasoline, fuel, parking, bus ticket, train ticket, taxi ride, uber ride, car maintenance, tires, and transportation-related expenses " +
                        "• Medical: medicine, vitamins, bandages, thermometer, prescription drugs, medical equipment, health supplements, and healthcare items " +
                        "• Stationery: pen, pencil, notebook, paper, stapler, scissors, calculator, books, educational materials, and office supplies " +
                        "• Personal care: makeup, perfume, deodorant, razor, moisturizer, sunscreen, hair products, skincare items, and grooming products " +
                        "• Recreation: movie tickets, games, sports equipment, toys, hobbies materials, subscription services, and entertainment items " +
                        "These are just examples - identify the EXACT item mentioned in the speech, even if it's not listed above. " +
                        "Examples: 'Got some cheese for 150 rupees' → item='cheese' (not 'food' or 'miscellaneous'). 'Bought medicine for 50 dollars' → item='medicine'. 'Filled my car' → item='gasoline'. " +
                        
                        "MERCHANT/PLACE IDENTIFICATION: Pay special attention to the word 'FROM', 'AT', or similar prepositions which indicate the merchant or place of purchase. " +
                        "Look for patterns like 'bought X from Y', 'got X from Y', 'purchased X from Y', 'picked up X from Y', 'bought X at Y', 'shopped at Y'. " +
                        "Examples: 'I bought milk from the corner store' → merchant='corner store'. 'Got coffee from Starbucks' → merchant='Starbucks'. " +
                        "'Purchased medicine from CVS pharmacy' → merchant='CVS pharmacy'. 'Bought clothes at the mall' → merchant='mall'. " +
                        "'Shopped at the supermarket' → merchant='supermarket'. 'Got lunch from the food court' → merchant='food court'. " +
                        "VALID MERCHANTS include: " +
                        "• Specific shop names: Starbucks, Walmart, Target, Amazon, eBay, CVS, Walgreens, Best Buy, Home Depot " +
                        "• Brand names: McDonald's, KFC, Subway, Pizza Hut, Domino's, Burger King " +
                        "• Supermarket names: Kroger, Safeway, Whole Foods, Costco, Sam's Club " +
                        "• Generic place types: mall, shopping center, supermarket, grocery store, convenience store, gas station, pharmacy, restaurant, cafe, bakery, " +
                        "corner store, local market, department store, outlet store, food court, street vendor, online store, farmers market, thrift store, bookstore " +
                        "If no specific place mentioned, infer from context: 'bought groceries' → 'grocery store', 'filled gas' → 'gas station', 'bought clothes' → 'clothing store', 'went shopping' → 'mall'."
                    },
                    new OpenAIMessage
                    {
                        Role = "user",
                        Content = speechText
                    }
                },
                Functions = new List<OpenAIFunction>
                {
                    new OpenAIFunction
                    {
                        Name = "extract_expense",
                        Description = "Extract the quantity, total monetary amount, currency, item description, merchant/place, and optional date from a free-form expense sentence. Extract both how many items AND how much money was spent.",
                        Parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                quantity = new { type = "number", nullable = true, description = "The number of items purchased - look for numbers immediately before item names. Examples: '2 liters milk' → quantity=2; '3 coffees' → quantity=3; '5 apples' → quantity=5. Default to 1 if not mentioned." },
                                amount = new { type = "number", description = "The monetary value paid - look for numbers with currency symbols/words. Examples: 'Rs 120' → amount=120; '$50' → amount=50; '150 rupees' → amount=150; '25 dollars' → amount=25. NEVER use the quantity number as amount. In '2 liters milk for Rs 120' - amount=120 (not 2)." },
                                currency = new { type = "string", description = "The currency type. Examples: 'Rs 120' or '120 rupees' → 'INR'; '$50' or '50 dollars' → 'USD'; '€30' or '30 euros' → 'EUR'. Look for currency symbols (Rs, $, €, £, ₹) or currency words (rupees, dollars, euros, pounds)." },
                                item = new { type = "string", description = "The specific item or product purchased. Can be ANY object people buy, including food items (cheese, milk, bread, pizza, fruits, vegetables), household items (soap, toilet paper, furniture, cookware), clothing (shirt, shoes, bags, accessories), electronics (phone, charger, TV, gaming devices), transport (gasoline, parking, tickets), medical (medicine, vitamins, healthcare items), stationery (pen, notebook, books), personal care (shampoo, makeup, skincare), recreation (movie tickets, games, sports equipment), and thousands of other possible items. Always extract the EXACT item name mentioned in the speech, regardless of category. Never use generic terms like 'food', 'miscellaneous', 'items', or 'stuff'." },
                                merchant = new { type = "string", nullable = true, description = "The place/store where the item was purchased. Look for words after these prepositions: 'FROM [place]' (e.g., 'from D-Mart' → 'D-Mart'), 'WENT TO [place]' (e.g., 'went to Walmart' → 'Walmart'), 'AT [place]' (e.g., 'at Starbucks' → 'Starbucks'). Examples: 'got milk from D-Mart' → merchant='D-Mart'; 'bought coffee at Starbucks' → merchant='Starbucks'; 'went to the mall' → merchant='mall'. Can be specific store names (D-Mart, Walmart, Starbucks) or generic places (mall, supermarket, pharmacy)." },
                                date = new { type = "string", nullable = true, description = "The date of the expense if mentioned (today, yesterday, specific date)" }
                            },
                            required = new[] { "quantity", "amount", "currency", "item" }
                        }
                    }
                },
                Function_call = "auto"
            };
        }

        private string NormalizeCurrency(string rawCurrency)
        {
            var currency = rawCurrency.Trim().ToUpper();

            return currency switch
            {
                "$" or "DOLLAR" or "DOLLARS" or "USD" or "US DOLLAR" or "US DOLLARS" => "USD",
                "€" or "EURO" or "EUROS" or "EUR" => "EUR",
                "£" or "POUND" or "POUNDS" or "GBP" or "BRITISH POUND" => "GBP",
                "¥" or "YEN" or "JPY" or "JAPANESE YEN" => "JPY",
                "₹" or "RUPEE" or "RUPEES" or "INR" or "INDIAN RUPEE" => "INR",
                "C$" or "CAD" or "CANADIAN DOLLAR" or "CANADIAN DOLLARS" => "CAD",
                "A$" or "AUD" or "AUSTRALIAN DOLLAR" or "AUSTRALIAN DOLLARS" => "AUD",
                "CHF" or "SWISS FRANC" or "SWISS FRANCS" => "CHF",
                "CNY" or "YUAN" or "CHINESE YUAN" or "RMB" => "CNY",
                "KRW" or "WON" or "KOREAN WON" => "KRW",
                _ => currency.Length == 3 && currency.All(char.IsLetter) ? currency : "USD"
            };
        }

        private string ProcessDate(string? dateString)
        {
            // If no date is provided or extracted, default to today's date
            if (string.IsNullOrWhiteSpace(dateString)) 
            {
                _logger.LogInformation("No date extracted from speech text, defaulting to today's date");
                return DateTime.Now.ToString("yyyy-MM-dd");
            }

            try
            {
                return dateString.ToLower() switch
                {
                    "today" => DateTime.Now.ToString("yyyy-MM-dd"),
                    "yesterday" => DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"),
                    "tomorrow" => DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"),
                    _ => DateTime.TryParse(dateString, out var parsedDate) 
                        ? parsedDate.ToString("yyyy-MM-dd") 
                        : DateTime.Now.ToString("yyyy-MM-dd") // Default to today if parsing fails
                };
            }
            catch
            {
                _logger.LogWarning("Failed to parse date '{DateString}', defaulting to today's date", dateString);
                return DateTime.Now.ToString("yyyy-MM-dd");
            }
        }
    }

    // Mock implementation for testing
    public class MockExpenseExtractionService : IExpenseExtractionService
    {
        private readonly ILogger<MockExpenseExtractionService> _logger;

        public MockExpenseExtractionService(ILogger<MockExpenseExtractionService> logger)
        {
            _logger = logger;
        }

        public async Task<ExtractedExpenseData?> ExtractExpenseAsync(string speechText)
        {
            try
            {
                // Simulate processing delay
                await Task.Delay(1500);

                var words = speechText.ToLower().Split(' ');
                var amountMatch = words.FirstOrDefault(w => System.Text.RegularExpressions.Regex.IsMatch(w, @"\d+(\.\d+)?"));
                var amount = double.TryParse(amountMatch, out var amt) ? amt : 0.0;

                // Mock currency detection
                var currency = words.Any(w => w.Contains("dollar") || w.Contains("$") || w.Contains("usd")) ? "USD" :
                              words.Any(w => w.Contains("euro") || w.Contains("€") || w.Contains("eur")) ? "EUR" :
                              words.Any(w => w.Contains("pound") || w.Contains("£") || w.Contains("gbp")) ? "GBP" :
                              words.Any(w => w.Contains("yen") || w.Contains("¥") || w.Contains("jpy")) ? "JPY" :
                              words.Any(w => w.Contains("rupee") || w.Contains("₹") || w.Contains("inr")) ? "INR" :
                              "USD";

                // Mock quantity detection
                var quantity = 1; // Default quantity
                var quantityPatterns = new[] { "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };
                var quantityNumbers = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                
                // Look for numbers or words that indicate quantity
                for (int i = 0; i < words.Length; i++)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(words[i], @"^\d+$"))
                    {
                        var num = int.Parse(words[i]);
                        // If the next word is an item (not currency), it's probably quantity
                        if (i + 1 < words.Length && !words[i + 1].Contains("dollar") && !words[i + 1].Contains("rupee"))
                        {
                            quantity = num;
                            break;
                        }
                    }
                    // Check for word-based quantities
                    for (int j = 0; j < quantityPatterns.Length; j++)
                    {
                        if (words[i].Contains(quantityPatterns[j]))
                        {
                            quantity = quantityNumbers[j];
                            break;
                        }
                    }
                }

                // Enhanced item detection for mock service
                var item = 
                    words.Any(w => w.Contains("coffee")) ? "coffee" :
                    words.Any(w => w.Contains("lunch") || w.Contains("meal")) ? "lunch" :
                    words.Any(w => w.Contains("dinner")) ? "dinner" :
                    words.Any(w => w.Contains("breakfast")) ? "breakfast" :
                    words.Any(w => w.Contains("food")) ? "food" :
                    words.Any(w => w.Contains("gas") || w.Contains("gasoline") || w.Contains("fuel")) ? "gasoline" :
                    words.Any(w => w.Contains("grocery") || w.Contains("groceries")) ? "groceries" :
                    words.Any(w => w.Contains("milk")) ? "milk" :
                    words.Any(w => w.Contains("bread")) ? "bread" :
                    words.Any(w => w.Contains("cheese")) ? "cheese" :
                    words.Any(w => w.Contains("apple") || w.Contains("apples")) ? "apples" :
                    words.Any(w => w.Contains("banana") || w.Contains("bananas")) ? "bananas" :
                    words.Any(w => w.Contains("snack") || w.Contains("snacks")) ? "snacks" :
                    words.Any(w => w.Contains("parking")) ? "parking" :
                    words.Any(w => w.Contains("taxi") || w.Contains("uber") || w.Contains("ride")) ? "transportation" :
                    words.Any(w => w.Contains("medicine") || w.Contains("medical") || w.Contains("pharmacy")) ? "medicine" :
                    words.Any(w => w.Contains("book") || w.Contains("books")) ? "books" :
                    words.Any(w => w.Contains("supplies") || w.Contains("office")) ? "office supplies" :
                    words.Any(w => w.Contains("clothes") || w.Contains("clothing") || w.Contains("shirt") || w.Contains("pants")) ? "clothing" :
                    "miscellaneous";

                var date = words.Contains("yesterday") ? DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd") :
                          words.Contains("today") ? DateTime.Now.ToString("yyyy-MM-dd") :
                          DateTime.Now.ToString("yyyy-MM-dd"); // Default to today if no date mentioned

                return new ExtractedExpenseData
                {
                    Quantity = quantity,
                    Amount = amount,
                    Item = item,
                    Date = date,
                    Currency = currency
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mock extraction failed for speech text: {SpeechText}", speechText);
                return null;
            }
        }
    }
}