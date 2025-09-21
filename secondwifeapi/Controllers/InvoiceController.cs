using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using secondwifeapi.Data;
using secondwifeapi.Models;
using secondwifeapi.Services;
using System.Text.Json;

namespace secondwifeapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoiceController : ControllerBase
    {
        private readonly string _storageConnectionString;
        private readonly string _containerName;
        private readonly string _queueName;
        private readonly ILogger<InvoiceController> _logger;
        private readonly ApplicationDbContext _context;

        public InvoiceController(IConfiguration configuration, ILogger<InvoiceController> logger, ApplicationDbContext context)
        {
            _storageConnectionString = configuration.GetConnectionString("AzureStorage") ?? 
                throw new ArgumentNullException(nameof(configuration), "AzureStorage connection string is required");
            _containerName = configuration["AzureStorage:ContainerName"] ?? "invoice-blob";
            _queueName = configuration["AzureStorage:QueueName"] ?? "expense-processing-queue";
            _logger = logger;
            _context = context;
        }

        [HttpPost("generate-sas-uri")]
        public IActionResult GenerateSasUri([FromBody] InvoiceUploadRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FileName))
                return BadRequest("File name is required.");

            try
            {
                var blobServiceClient = new BlobServiceClient(_storageConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
                var blobClient = containerClient.GetBlobClient(request.FileName);

                var startsOn = DateTimeOffset.UtcNow.AddDays(-1);
                var expiresOn = DateTimeOffset.UtcNow.AddDays(1);
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = _containerName,
                    BlobName = request.FileName,
                    Resource = "b",
                    StartsOn = startsOn,
                    ExpiresOn = expiresOn
                };
                sasBuilder.SetPermissions(BlobSasPermissions.All);

                var sasUri = blobClient.GenerateSasUri(sasBuilder);
                var response = new InvoiceUploadResponse
                {
                    UploadId = Guid.NewGuid().ToString(),
                    SasUrl = sasUri.ToString(),
                    ExpiresAt = expiresOn.ToUnixTimeMilliseconds()
                };

                _logger.LogInformation("Generated SAS URI for file: {FileName}, UploadId: {UploadId}", request.FileName, response.UploadId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating SAS URI for file: {FileName}", request.FileName);
                return StatusCode(500, "Error generating upload URL");
            }
        }

        [HttpPost("save-expense")]
        public async Task<IActionResult> SaveExpense([FromBody] SaveExpenseRequest request)
        {
            _logger.LogInformation("SaveExpense called with GroupId: {GroupId}, UserId: {UserId}, BlobSasUrl: {BlobSasUrl}", 
                request.GroupId, request.UserId, request.BlobSasUrl);

            if (request.GroupId <= 0 || 
                request.UserId <= 0 || 
                string.IsNullOrWhiteSpace(request.BlobSasUrl))
            {
                _logger.LogWarning("Invalid request parameters: GroupId={GroupId}, UserId={UserId}, BlobSasUrl={BlobSasUrl}", 
                    request.GroupId, request.UserId, request.BlobSasUrl);
                return BadRequest("GroupId, UserId and BlobSasUrl are required.");
            }

            try
            {
                // Log connection string (without sensitive parts)
                var connStringMasked = string.IsNullOrEmpty(_storageConnectionString) ? "NULL" : 
                    _storageConnectionString.Length > 50 ? _storageConnectionString.Substring(0, 50) + "..." : "SHORT";
                _logger.LogInformation("Using storage connection: {ConnString}, Queue name: {QueueName}", 
                    connStringMasked, _queueName);

                // Generate job ID
                var jobId = Guid.NewGuid().ToString();
                _logger.LogInformation("Generated JobId: {JobId}", jobId);

                // Construct webhook URL automatically if not provided
                var webhookUrl = request.WebhookUrl;
                if (string.IsNullOrWhiteSpace(webhookUrl))
                {
                    // Build the webhook URL based on the current request
                    var scheme = HttpContext.Request.Scheme;
                    var host = HttpContext.Request.Host;
                    webhookUrl = $"{scheme}://{host}/api/invoice/webhook-callback";
                    _logger.LogInformation("Auto-generated webhook URL: {WebhookUrl}", webhookUrl);
                }

                // Create message for queue processing
                var message = new ProcessExpenseMessage
                {
                    JobId = jobId,
                    GroupId = request.GroupId,
                    UserId = request.UserId,
                    BlobSasUrl = request.BlobSasUrl,
                    WebhookUrl = webhookUrl, // Always include webhook URL
                    CreatedAt = DateTime.UtcNow
                };

                // Add message to Azure Storage Queue
                _logger.LogInformation("Creating queue service client...");
                var queueServiceClient = new QueueServiceClient(_storageConnectionString);
                
                _logger.LogInformation("Getting queue client for queue: {QueueName}", _queueName);
                var queueClient = queueServiceClient.GetQueueClient(_queueName);
                
                _logger.LogInformation("Creating queue if not exists...");
                var createResponse = await queueClient.CreateIfNotExistsAsync();
                _logger.LogInformation("Queue creation completed");

                var messageJson = JsonSerializer.Serialize(message);
                _logger.LogInformation("Serialized message length: {Length} characters", messageJson.Length);
                _logger.LogInformation("Message content: {Message}", messageJson);

                _logger.LogInformation("Sending message to queue...");
                var sendResponse = await queueClient.SendMessageAsync(messageJson);
                _logger.LogInformation("Message sent successfully! MessageId: {MessageId}", 
                    sendResponse.Value.MessageId);

                // Return immediate response
                var response = new SaveExpenseResponse
                {
                    JobId = jobId,
                    Status = "pending",
                    Message = "Receipt is being processed. You will be notified."
                };

                _logger.LogInformation("Queued expense processing for JobId: {JobId}", jobId);
                return Ok(response);
            }
            catch (Azure.RequestFailedException azureEx)
            {
                _logger.LogError(azureEx, "Azure Storage error: {ErrorCode} - {Message}", azureEx.ErrorCode, azureEx.Message);
                return StatusCode(500, $"Azure Storage error: {azureEx.ErrorCode} - {azureEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queuing expense processing: {Message}", ex.Message);
                return StatusCode(500, $"Error queuing expense processing: {ex.Message}");
            }
        }

        [HttpPost("webhook-callback")]
        public async Task<IActionResult> WebhookCallback([FromBody] WebhookCallbackRequest request)
        {
            _logger.LogInformation("Webhook callback received for JobId: {JobId}, Status: {Status}", 
                request.JobId, request.Status);

            if (string.IsNullOrWhiteSpace(request.JobId))
            {
                _logger.LogWarning("Webhook callback received with empty JobId");
                return BadRequest(new WebhookCallbackResponse
                {
                    Success = false,
                    Message = "JobId is required."
                });
            }

            try
            {
                if (request.Status == "completed" && request.InvoiceData != null)
                {
                    _logger.LogInformation("Processing completed successfully for JobId: {JobId}. " +
                        "Vendor: {VendorName}, Total: {InvoiceTotal}, Date: {InvoiceDate}, Items: {ItemCount}",
                        request.JobId,
                        request.InvoiceData.VendorName ?? "N/A",
                        request.InvoiceData.InvoiceTotal?.ToString("C") ?? "N/A",
                        request.InvoiceData.InvoiceDate?.ToString("yyyy-MM-dd") ?? "N/A",
                        request.InvoiceData.Items?.Count ?? 0);

                    // Save expense data to database
                    await SaveExpenseToDatabase(request);
                }
                else if (request.Status == "failed")
                {
                    _logger.LogError("Processing failed for JobId: {JobId}. Error: {Error}",
                        request.JobId, request.Error ?? "Unknown error");

                    // Here you can add logic to:
                    // 1. Update job status in database
                    // 2. Send error notifications to users
                    // 3. Trigger retry mechanisms or error handling workflows
                }
                else
                {
                    _logger.LogWarning("Unknown status received for JobId: {JobId}, Status: {Status}",
                        request.JobId, request.Status);
                }

                var response = new WebhookCallbackResponse
                {
                    Success = true,
                    Message = $"Webhook processed successfully for job {request.JobId}",
                    JobId = request.JobId
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook callback for JobId: {JobId}", request.JobId);
                return StatusCode(500, new WebhookCallbackResponse
                {
                    Success = false,
                    Message = "Error processing webhook callback."
                });
            }
        }

        [HttpPost("manual-expense")]
        public async Task<IActionResult> CreateManualExpense([FromBody] ManualExpenseRequest request)
        {
            try
            {
                _logger.LogInformation("Creating manual expense for User: {UserId}, Group: {GroupId}, Vendor: {VendorName}", 
                    request.UserId, request.GroupId, request.VendorName);

                // Validate required fields
                if (request.UserId <= 0 || request.GroupId <= 0 || string.IsNullOrWhiteSpace(request.ItemName) || request.Price <= 0)
                {
                    return BadRequest(new { Message = "UserId, GroupId, ItemName, and Price are required and must be valid." });
                }

                var expenseDate = request.Date?.Date ?? DateTime.UtcNow.Date;
                var currency = !string.IsNullOrWhiteSpace(request.Currency) ? request.Currency : "USD";

                // Check if expense already exists for this user, group and date
                var existingExpense = await _context.Expenses
                    .FirstOrDefaultAsync(e => e.UserId == request.UserId && e.GroupId == request.GroupId && e.ExpenseDate.Date == expenseDate);

                Expense expense;
                if (existingExpense != null)
                {
                    // Update existing expense
                    expense = existingExpense;
                    expense.TotalAmount += request.Price;
                    expense.Currency = currency; // Update to latest currency if needed
                    expense.VendorName = string.IsNullOrEmpty(expense.VendorName) 
                        ? request.VendorName 
                        : $"{expense.VendorName}, {request.VendorName}";
                    expense.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Create new expense
                    expense = new Expense
                    {
                        UserId = request.UserId,
                        GroupId = request.GroupId,
                        ExpenseDate = expenseDate,
                        TotalAmount = request.Price,
                        Currency = currency,
                        VendorName = request.VendorName ?? "Manual Entry",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Expenses.Add(expense);
                }

                // Save changes to get the ExpenseId
                await _context.SaveChangesAsync();

                // Add expense item
                var expenseItem = new ExpenseItem
                {
                    ExpenseId = expense.ExpenseId,
                    Description = request.ItemName,
                    Amount = request.Price,
                    Quantity = 1,
                    Currency = currency,
                    CreatedAt = DateTime.UtcNow
                };
                _context.ExpenseItems.Add(expenseItem);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully created manual expense for User: {UserId}, Date: {Date}, Amount: {Amount}",
                    request.UserId, expenseDate.ToString("yyyy-MM-dd"), request.Price);

                var response = new ManualExpenseResponse
                {
                    Success = true,
                    Message = "Manual expense created successfully",
                    ExpenseId = expense.ExpenseId.ToString(),
                    TotalAmount = expense.TotalAmount,
                    Currency = expense.Currency,
                    Date = expenseDate
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create manual expense for User: {UserId}", request.UserId);
                return StatusCode(500, new ManualExpenseResponse
                {
                    Success = false,
                    Message = "Error creating manual expense."
                });
            }
        }

        private async Task SaveExpenseToDatabase(WebhookCallbackRequest request)
        {
            try
            {
                var invoiceData = request.InvoiceData;
                if (invoiceData == null || request.UserId == 0)
                {
                    _logger.LogWarning("No valid invoice data or user ID found for JobId: {JobId}", request.JobId);
                    return;
                }

                var userId = request.UserId;
                var expenseDate = invoiceData.InvoiceDate ?? DateTime.UtcNow;

                // Check if expense already exists for this user, group and date
                var existingExpense = await _context.Expenses
                    .FirstOrDefaultAsync(e => e.UserId == userId && e.GroupId == request.GroupId && e.ExpenseDate.Date == expenseDate.Date);

                Expense expense;
                if (existingExpense != null)
                {
                    // Update existing expense
                    expense = existingExpense;
                    expense.TotalAmount += invoiceData.InvoiceTotal ?? 0m;
                    expense.Currency = invoiceData.Currency ?? expense.Currency;
                    expense.VendorName = string.IsNullOrEmpty(expense.VendorName) 
                        ? invoiceData.VendorName 
                        : $"{expense.VendorName}, {invoiceData.VendorName}";
                    expense.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Create new expense
                    expense = new Expense
                    {
                        UserId = userId,
                        GroupId = request.GroupId,
                        ExpenseDate = expenseDate.Date,
                        TotalAmount = invoiceData.InvoiceTotal ?? 0m,
                        Currency = invoiceData.Currency ?? "USD",
                        VendorName = invoiceData.VendorName,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Expenses.Add(expense);
                }

                // Save changes to get the ExpenseId
                await _context.SaveChangesAsync();

                // Add expense items
                if (invoiceData.Items?.Any() == true)
                {
                    foreach (var item in invoiceData.Items)
                    {
                        var expenseItem = new ExpenseItem
                        {
                            ExpenseId = expense.ExpenseId,
                            Description = item.Description ?? "Unknown Item",
                            Amount = item.Amount ?? 0m,
                            Quantity = item.Quantity ?? 1,
                            Currency = invoiceData.Currency ?? "USD",
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.ExpenseItems.Add(expenseItem);
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully saved expense data to database for User: {UserId}, Date: {Date}, Total: {Total}",
                    userId, expenseDate.Date.ToString("yyyy-MM-dd"), expense.TotalAmount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save expense data to database for JobId: {JobId}", request.JobId);
                throw;
            }
        }

        [HttpPost("voice-expense")]
        public async Task<IActionResult> CreateVoiceExpense([FromBody] VoiceExpenseRequest request, 
            [FromServices] IExpenseExtractionService extractionService)
        {
            try
            {
                _logger.LogInformation("Creating voice expense for User: {UserId}, Group: {GroupId}, Speech: {SpeechText}", 
                    request.UserId, request.GroupId, request.SpeechText);

                // Validate request
                if (request.UserId <= 0 || request.GroupId <= 0 || string.IsNullOrWhiteSpace(request.SpeechText))
                {
                    return BadRequest(new { Message = "UserId, GroupId, and SpeechText are required and must be valid." });
                }

                // Extract expense data from speech text using Azure OpenAI
                _logger.LogInformation("Extracting expense data from speech: {SpeechText}", request.SpeechText);
                var extractedData = await extractionService.ExtractExpenseAsync(request.SpeechText);
                if (extractedData == null)
                {
                    _logger.LogWarning("Failed to extract expense information from speech text: {SpeechText}", request.SpeechText);
                    return BadRequest(new { Message = "Failed to extract expense information from speech text. Please try rephrasing your expense description." });
                }

                _logger.LogInformation("Successfully extracted expense data: Amount={Amount}, Currency={Currency}, Item={Item}, Merchant={Merchant}, Date={Date}", 
                    extractedData.Amount, extractedData.Currency, extractedData.Item, extractedData.Merchant, extractedData.Date);

                // Convert extracted data to expense
                var expenseDate = !string.IsNullOrEmpty(extractedData.Date) 
                    ? DateTime.TryParse(extractedData.Date, out var parsedDate) 
                        ? parsedDate.Date 
                        : DateTime.UtcNow.Date
                    : DateTime.UtcNow.Date;

                // Validate extracted data
                if (extractedData.Amount <= 0)
                {
                    _logger.LogWarning("Invalid amount extracted: {Amount}", extractedData.Amount);
                    return BadRequest(new { Message = "Could not extract a valid amount from the speech text. Please specify the amount clearly." });
                }

                if (string.IsNullOrWhiteSpace(extractedData.Item))
                {
                    _logger.LogWarning("Invalid item extracted: {Item}", extractedData.Item);
                    return BadRequest(new { Message = "Could not extract a valid item description from the speech text." });
                }

                // Use transaction to ensure data consistency
                using var transaction = await _context.Database.BeginTransactionAsync();
                Expense expense;
                try
                {
                    // Always create a new expense for each voice entry
                    // Each voice input represents a distinct transaction/purchase
                    expense = new Expense
                    {
                        UserId = request.UserId,
                        GroupId = request.GroupId,
                        ExpenseDate = expenseDate,
                        TotalAmount = (decimal)extractedData.Amount,
                        Currency = extractedData.Currency,
                        VendorName = extractedData.Merchant ?? "Voice Entry",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Expenses.Add(expense);
                    
                    _logger.LogInformation("Created new expense from voice input: Amount={Amount}, Currency={Currency}, Vendor={Vendor}", 
                        expense.TotalAmount, expense.Currency, expense.VendorName);

                    // Save changes to get the ExpenseId
                    await _context.SaveChangesAsync();

                    // Add expense item with all extracted data
                    var expenseItem = new ExpenseItem
                    {
                        ExpenseId = expense.ExpenseId,
                        Description = $"{extractedData.Item}" + 
                                   (!string.IsNullOrEmpty(extractedData.Merchant) ? $" from {extractedData.Merchant}" : "") +
                                   " (Voice Entry)",
                        Amount = (decimal)extractedData.Amount,
                        Quantity = extractedData.Quantity,
                        Currency = extractedData.Currency,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.ExpenseItems.Add(expenseItem);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Successfully saved voice expense to database: ExpenseId={ExpenseId}, ExpenseItemId={ExpenseItemId}", 
                        expense.ExpenseId, expenseItem.ExpenseItemId);
                }
                catch (Exception dbEx)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(dbEx, "Failed to save voice expense to database for User: {UserId}", request.UserId);
                    throw; // Re-throw to be caught by outer exception handler
                }

                // Reload the expense from database to get the latest data including the new expense item
                var savedExpense = await _context.Expenses
                    .Include(e => e.ExpenseItems)
                    .FirstAsync(e => e.ExpenseId == expense.ExpenseId);

                _logger.LogInformation("Successfully created voice expense for User: {UserId}, Date: {Date}, Amount: {Amount}, Total Items: {ItemCount}",
                    request.UserId, expenseDate.ToString("yyyy-MM-dd"), extractedData.Amount, savedExpense.ExpenseItems.Count);

                var response = new VoiceExpenseResponse
                {
                    Success = true,
                    Message = $"Voice expense created successfully. Saved to database with {savedExpense.ExpenseItems.Count} item(s).",
                    ExpenseId = savedExpense.ExpenseId.ToString(),
                    ExtractedData = extractedData,
                    TotalAmount = (decimal)extractedData.Amount, // Show the current extracted amount, not accumulated total
                    Currency = extractedData.Currency, // Use extracted currency for consistency
                    Date = expenseDate
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create voice expense for User: {UserId}", request.UserId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error creating voice expense."
                });
            }
        }
    }
}
