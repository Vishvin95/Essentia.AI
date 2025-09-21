using Azure.Storage.Queues;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace secondwifeapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TestController> _logger;

        public TestController(IConfiguration configuration, ILogger<TestController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("test-queue")]
        public async Task<IActionResult> TestQueue()
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("AzureStorage");
                var queueName = _configuration["AzureStorage:QueueName"] ?? "expense-processing-queue";

                _logger.LogInformation("Testing queue connection...");
                _logger.LogInformation("Connection string length: {Length}", connectionString?.Length ?? 0);
                _logger.LogInformation("Queue name: {QueueName}", queueName);

                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest("Azure Storage connection string is missing");
                }

                // Test 1: Create QueueServiceClient
                var queueServiceClient = new QueueServiceClient(connectionString);
                _logger.LogInformation("✓ QueueServiceClient created");

                // Test 2: Get queue client
                var queueClient = queueServiceClient.GetQueueClient(queueName);
                _logger.LogInformation("✓ QueueClient created for queue: {QueueName}", queueName);

                // Test 3: Check if queue exists
                var exists = await queueClient.ExistsAsync();
                _logger.LogInformation("Queue exists: {Exists}", exists.Value);

                // Test 4: Create queue if it doesn't exist
                if (!exists.Value)
                {
                    var createResponse = await queueClient.CreateAsync();
                    _logger.LogInformation("✓ Queue created successfully");
                }
                else
                {
                    _logger.LogInformation("✓ Queue already exists");
                }

                // Test 5: Send a test message
                var testMessage = new { 
                    TestId = Guid.NewGuid().ToString(),
                    Timestamp = DateTime.UtcNow,
                    Message = "Test queue connectivity"
                };

                var messageJson = JsonSerializer.Serialize(testMessage);
                var sendResponse = await queueClient.SendMessageAsync(messageJson);
                _logger.LogInformation("✓ Test message sent! MessageId: {MessageId}", 
                    sendResponse.Value.MessageId);

                // Test 6: Get queue properties
                var properties = await queueClient.GetPropertiesAsync();
                _logger.LogInformation("Queue message count: {Count}", 
                    properties.Value.ApproximateMessagesCount);

                return Ok(new {
                    Status = "Success",
                    QueueExists = exists.Value,
                    MessageId = sendResponse.Value.MessageId,
                    ApproximateMessageCount = properties.Value.ApproximateMessagesCount,
                    TestMessage = testMessage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Queue test failed: {Message}", ex.Message);
                return StatusCode(500, new { 
                    Error = ex.Message,
                    Type = ex.GetType().Name,
                    StackTrace = ex.StackTrace?.Split('\n').Take(5).ToArray()
                });
            }
        }
    }
}