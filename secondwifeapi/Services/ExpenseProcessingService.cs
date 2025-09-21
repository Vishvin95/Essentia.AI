using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.Storage.Queues;
using secondwifeapi.Models;
using secondwifeapi.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Azure;

namespace secondwifeapi.Services
{
    public class ExpenseProcessingService : BackgroundService
    {
        private readonly string _storageConnectionString;
        private readonly string _queueName;
        private readonly string _documentIntelligenceEndpoint;
        private readonly string _documentIntelligenceKey;
        private readonly ILogger<ExpenseProcessingService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IServiceProvider _serviceProvider;

        public ExpenseProcessingService(
            IConfiguration configuration,
            ILogger<ExpenseProcessingService> logger,
            HttpClient httpClient,
            IServiceProvider serviceProvider)
        {
            _storageConnectionString = configuration.GetConnectionString("AzureStorage") ?? 
                throw new ArgumentNullException(nameof(configuration), "AzureStorage connection string is required");
            _documentIntelligenceEndpoint = configuration["DocumentIntelligence:Endpoint"] ?? 
                throw new ArgumentNullException(nameof(configuration), "DocumentIntelligence endpoint is required");
            _documentIntelligenceKey = configuration["DocumentIntelligence:Key"] ?? 
                throw new ArgumentNullException(nameof(configuration), "DocumentIntelligence key is required");
            _queueName = configuration["AzureStorage:QueueName"] ?? "expense-processing-queue";
            _logger = logger;
            _httpClient = httpClient;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var queueServiceClient = new QueueServiceClient(_storageConnectionString);
            var queueClient = queueServiceClient.GetQueueClient(_queueName);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Check for messages in the queue
                    var messages = await queueClient.ReceiveMessagesAsync(maxMessages: 10);

                    if (messages?.Value?.Length == 0)
                    {
                        // No messages, wait before checking again
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        continue;
                    }

                    if (messages?.Value != null)
                    {
                        foreach (var message in messages.Value)
                        {
                            try
                            {
                                if (!string.IsNullOrEmpty(message.MessageText))
                                {
                                    await ProcessMessage(message.MessageText, queueClient, message);
                                }
                                else
                                {
                                    _logger.LogWarning("Received empty message with ID: {MessageId}", message.MessageId);
                                    // Delete empty messages
                                    await SafeDeleteMessageAsync(queueClient, message);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing message: {MessageId}. Message will be left in queue for retry or manual handling.", message.MessageId);
                                // Don't delete the message here - let it be retried or handled manually
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in background service execution");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
        }

        private async Task ProcessMessage(string messageText, QueueClient queueClient, Azure.Storage.Queues.Models.QueueMessage queueMessage)
        {
            ProcessExpenseMessage? expenseMessage = null;
            bool messageDeleted = false;
            
            try
            {
                expenseMessage = JsonSerializer.Deserialize<ProcessExpenseMessage>(messageText);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize ProcessExpenseMessage from message text: {MessageText}", messageText);
                messageDeleted = await SafeDeleteMessageAsync(queueClient, queueMessage);
                return;
            }

            if (expenseMessage == null)
            {
                _logger.LogWarning("ProcessExpenseMessage deserialized to null from message text: {MessageText}", messageText);
                messageDeleted = await SafeDeleteMessageAsync(queueClient, queueMessage);
                return;
            }

            try
            {
                // Use SAS URL directly from the message
                if (string.IsNullOrWhiteSpace(expenseMessage.BlobSasUrl))
                {
                    _logger.LogWarning("BlobSasUrl is null or empty in expenseMessage");
                    messageDeleted = await SafeDeleteMessageAsync(queueClient, queueMessage);
                    return;
                }

                var sasUrl = expenseMessage.BlobSasUrl;

                // Initialize Document Intelligence client
                var credential = new AzureKeyCredential(_documentIntelligenceKey);
                var client = new DocumentAnalysisClient(new Uri(_documentIntelligenceEndpoint), credential);

                // Get user's default currency for fallback
                string userDefaultCurrency = "USD"; // Default fallback
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == expenseMessage.UserId);
                    if (user != null)
                    {
                        userDefaultCurrency = user.DefaultCurrency;
                        _logger.LogInformation("Retrieved user {UserId} default currency: {Currency}", 
                            expenseMessage.UserId, userDefaultCurrency);
                    }
                }

                // Analyze document using prebuilt-invoice model
                var operation = await client.AnalyzeDocumentFromUriAsync(WaitUntil.Completed, "prebuilt-invoice", new Uri(sasUrl));
                var result = operation.Value;

                // Extract invoice data with null safety
                var firstDoc = result.Documents?.FirstOrDefault();
                
                // Extract currency from document, fallback to user's default currency
                var extractedCurrency = GetCurrencyFromDocument(firstDoc) ?? userDefaultCurrency;

                var extractedData = new
                {
                    JobId = expenseMessage.JobId,
                    GroupId = expenseMessage.GroupId,
                    UserId = expenseMessage.UserId,
                    BlobSasUrl = expenseMessage.BlobSasUrl,
                    Status = "completed",
                    ProcessedAt = DateTime.UtcNow,
                    InvoiceData = new
                    {
                        VendorName = GetFieldValue(firstDoc, "VendorName", field => field.Value?.AsString()),
                        InvoiceTotal = GetFieldValue(firstDoc, "InvoiceTotal", field => field.Value?.AsCurrency().Amount),
                        Currency = extractedCurrency,
                        InvoiceDate = GetFieldValue(firstDoc, "InvoiceDate", field => field.Value?.AsDate()),
                        Items = GetItemsData(firstDoc, extractedCurrency)
                    }
                };

                // Send webhook notification if URL is provided
                if (!string.IsNullOrWhiteSpace(expenseMessage.WebhookUrl))
                {
                    await SendWebhookNotification(expenseMessage.WebhookUrl, extractedData);
                }

                _logger.LogInformation("Successfully processed expense with JobId: {JobId}", expenseMessage.JobId);

                // Delete message from queue after successful processing
                if (!messageDeleted)
                {
                    messageDeleted = await SafeDeleteMessageAsync(queueClient, queueMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expense with JobId: {JobId}", expenseMessage?.JobId ?? "unknown");

                // Send error notification via webhook if URL is provided
                if (!string.IsNullOrWhiteSpace(expenseMessage?.WebhookUrl))
                {
                    var errorData = new
                    {
                        JobId = expenseMessage.JobId,
                        GroupId = expenseMessage.GroupId,
                        UserId = expenseMessage.UserId,
                        BlobSasUrl = expenseMessage.BlobSasUrl,
                        Status = "failed",
                        ProcessedAt = DateTime.UtcNow,
                        Error = ex.Message
                    };
                    
                    try
                    {
                        await SendWebhookNotification(expenseMessage.WebhookUrl, errorData);
                    }
                    catch (Exception webhookEx)
                    {
                        _logger.LogError(webhookEx, "Failed to send error webhook notification for JobId: {JobId}", expenseMessage.JobId);
                    }
                }

                // Delete message from queue to avoid infinite retries
                if (!messageDeleted)
                {
                    messageDeleted = await SafeDeleteMessageAsync(queueClient, queueMessage);
                }
            }
        }

        private static T? GetFieldValue<T>(AnalyzedDocument? document, string fieldName, Func<DocumentField, T?> valueExtractor)
        {
            if (document?.Fields?.ContainsKey(fieldName) == true)
            {
                return valueExtractor(document.Fields[fieldName]);
            }
            return default;
        }

        private static List<object>? GetItemsData(AnalyzedDocument? document, string fallbackCurrency)
        {
            if (document?.Fields?.ContainsKey("Items") != true)
                return null;

            var itemsField = document.Fields["Items"];
            var itemsList = itemsField.Value?.AsList();
            
            if (itemsList == null)
                return null;

            return itemsList
                .Select(item =>
                {
                    var dict = item.Value?.AsDictionary();
                    if (dict == null)
                        return (object)new { 
                            Description = (string?)null, 
                            Amount = (decimal?)null, 
                            Currency = fallbackCurrency,
                            Quantity = (double?)null 
                        };

                    // Try to extract currency from item, fallback to invoice currency
                    var itemCurrency = fallbackCurrency;
                    if (dict.ContainsKey("Amount"))
                    {
                        var amountField = dict["Amount"];
                        var currencyFromItem = ExtractCurrencyFromField(amountField);
                        if (!string.IsNullOrWhiteSpace(currencyFromItem))
                        {
                            itemCurrency = currencyFromItem;
                        }
                    }

                    return (object)new
                    {
                        Description = dict.ContainsKey("Description") ? dict["Description"].Value?.AsString() : null,
                        Amount = dict.ContainsKey("Amount") ? dict["Amount"].Value?.AsCurrency().Amount : null,
                        Currency = itemCurrency,
                        Quantity = dict.ContainsKey("Quantity") ? dict["Quantity"].Value?.AsDouble() : null
                    };
                })
                .ToList();
        }

        private static string? GetCurrencyFromDocument(AnalyzedDocument? document)
        {
            if (document?.Fields?.ContainsKey("InvoiceTotal") == true)
            {
                var totalField = document.Fields["InvoiceTotal"];
                return ExtractCurrencyFromField(totalField);
            }

            // Try other currency-related fields
            var currencyFields = new[] { "CurrencyCode", "Currency", "SubTotal", "TotalTax" };
            foreach (var fieldName in currencyFields)
            {
                if (document?.Fields?.ContainsKey(fieldName) == true)
                {
                    var currency = ExtractCurrencyFromField(document.Fields[fieldName]);
                    if (!string.IsNullOrWhiteSpace(currency))
                        return currency;
                }
            }

            return null;
        }

        private static string? ExtractCurrencyFromField(DocumentField field)
        {
            try
            {
                if (field.FieldType == DocumentFieldType.Currency)
                {
                    var currencyValue = field.Value?.AsCurrency();
                    if (currencyValue != null)
                    {
                        // Try to extract currency from the raw text or use common mappings
                        var rawText = field.Content;
                        if (!string.IsNullOrWhiteSpace(rawText))
                        {
                            // Common currency symbol to code mappings
                            var currencyMappings = new Dictionary<string, string>
                            {
                                { "$", "USD" }, { "€", "EUR" }, { "£", "GBP" }, { "¥", "JPY" },
                                { "₹", "INR" }, { "₽", "RUB" }, { "¢", "USD" }, { "₦", "NGN" },
                                { "₡", "CRC" }, { "₨", "PKR" }, { "₩", "KRW" }
                            };

                            foreach (var mapping in currencyMappings)
                            {
                                if (rawText.Contains(mapping.Key))
                                    return mapping.Value;
                            }

                            // Look for 3-letter currency codes in the text
                            var words = rawText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var word in words)
                            {
                                if (word.Length == 3 && word.All(char.IsLetter))
                                {
                                    var upperWord = word.ToUpper();
                                    if (IsValidCurrencyCode(upperWord))
                                        return upperWord;
                                }
                            }
                        }
                    }
                }
                else if (field.FieldType == DocumentFieldType.String)
                {
                    var text = field.Value?.AsString();
                    if (!string.IsNullOrWhiteSpace(text) && text.Length == 3 && text.All(char.IsLetter))
                    {
                        var upperText = text.ToUpper();
                        if (IsValidCurrencyCode(upperText))
                            return upperText;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore extraction errors and fallback
            }

            return null;
        }

        private static bool IsValidCurrencyCode(string currencyCode)
        {
            if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
                return false;

            var validCurrencies = new HashSet<string>
            {
                "USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF", "CNY", "SEK", "NZD",
                "MXN", "SGD", "HKD", "NOK", "TRY", "ZAR", "BRL", "INR", "KRW", "PLN",
                "DKK", "CZK", "HUF", "ILS", "CLP", "PHP", "AED", "COP", "SAR", "MYR",
                "RON", "THB", "BGN", "HRK", "RUB", "ISK", "IDR", "UAH"
            };

            return validCurrencies.Contains(currencyCode.ToUpper());
        }

        private async Task<bool> SafeDeleteMessageAsync(QueueClient queueClient, Azure.Storage.Queues.Models.QueueMessage queueMessage)
        {
            try
            {
                await queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt);
                _logger.LogDebug("Successfully deleted message {MessageId}", queueMessage.MessageId);
                return true;
            }
            catch (Azure.RequestFailedException ex) when (ex.ErrorCode == "MessageNotFound")
            {
                // Message was already deleted, this is expected in some scenarios
                _logger.LogDebug("Message {MessageId} was already deleted: {Error}", queueMessage.MessageId, ex.Message);
                return true; // Consider this successful since the goal is achieved
            }
            catch (Azure.RequestFailedException ex)
            {
                _logger.LogError(ex, "Failed to delete message {MessageId}: {ErrorCode} - {Message}", 
                    queueMessage.MessageId, ex.ErrorCode, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting message {MessageId}", queueMessage.MessageId);
                return false;
            }
        }

        private async Task SendWebhookNotification(string webhookUrl, object data)
        {
            try
            {
                var json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(webhookUrl, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Webhook notification failed with status: {StatusCode}", response.StatusCode);
                }
                else
                {
                    _logger.LogInformation("Webhook notification sent successfully to: {WebhookUrl}", webhookUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending webhook notification to: {WebhookUrl}", webhookUrl);
            }
        }
    }
}