using Microsoft.AspNetCore.Mvc;
using secondwifeapi.Models;
using secondwifeapi.Services;

namespace secondwifeapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserContextController : ControllerBase
    {
        private readonly IContextExtractionService _contextExtractionService;
        private readonly ICosmosDbService _cosmosDbService;
        private readonly ILogger<UserContextController> _logger;

        public UserContextController(
            IContextExtractionService contextExtractionService,
            ICosmosDbService cosmosDbService,
            ILogger<UserContextController> logger)
        {
            _contextExtractionService = contextExtractionService;
            _cosmosDbService = cosmosDbService;
            _logger = logger;
        }

        /// <summary>
        /// Process user context text and save structured context to Cosmos DB
        /// </summary>
        /// <param name="request">User context request containing userId and context text</param>
        /// <returns>Processed and saved user context</returns>
        [HttpPost("process-context")]
        public async Task<ActionResult<UserContextResponse>> ProcessUserContext([FromBody] UserContextRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ContextText))
                {
                    return BadRequest("Context text is required");
                }

                if (request.UserId <= 0)
                {
                    return BadRequest("Valid UserId is required");
                }

                _logger.LogInformation($"Processing context for user {request.UserId}");

                // Extract structured context using Azure OpenAI
                var openAIRequest = new OpenAIContextRequest
                {
                    UserText = request.ContextText,
                    UserId = request.UserId,
                    CurrentDate = DateTime.UtcNow
                };

                var extractionResult = await _contextExtractionService.ExtractStructuredContextAsync(openAIRequest);

                if (!extractionResult.Success)
                {
                    _logger.LogError($"Failed to extract structured context: {extractionResult.ErrorMessage}");
                    return StatusCode(500, $"Failed to process context: {extractionResult.ErrorMessage}");
                }

                // Generate unique context ID
                var contextId = $"CTX_{DateTime.UtcNow:yyyyMMdd}_{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

                // Create UserContext object
                var userContext = new UserContext
                {
                    UserId = request.UserId,
                    ContextId = contextId,
                    ContextText = request.ContextText,
                    StructuredContext = extractionResult.StructuredContext,
                    IsActive = true
                };

                // Save to Cosmos DB
                var savedContext = await _cosmosDbService.SaveUserContextAsync(userContext);

                var response = new UserContextResponse
                {
                    ContextId = contextId,
                    Message = "User context processed and saved successfully",
                    SavedContext = savedContext
                };

                _logger.LogInformation($"Successfully processed and saved context {contextId} for user {request.UserId}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing user context for user {request.UserId}");
                return StatusCode(500, "An error occurred while processing user context");
            }
        }

        /// <summary>
        /// Get a specific user context by context ID
        /// </summary>
        /// <param name="contextId">The context ID to retrieve</param>
        /// <param name="userId">The user ID</param>
        /// <returns>User context if found</returns>
        [HttpGet("context/{contextId}/user/{userId}")]
        public async Task<ActionResult<UserContext>> GetUserContext(string contextId, int userId)
        {
            try
            {
                _logger.LogInformation($"Retrieving context {contextId} for user {userId}");

                var userContext = await _cosmosDbService.GetUserContextAsync(contextId, userId);

                if (userContext == null)
                {
                    return NotFound($"Context {contextId} not found for user {userId}");
                }

                return Ok(userContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving context {contextId} for user {userId}");
                return StatusCode(500, "An error occurred while retrieving user context");
            }
        }

        /// <summary>
        /// Get all contexts for a specific user
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="limit">Maximum number of contexts to return (default: 50)</param>
        /// <returns>List of user contexts</returns>
        [HttpGet("user/{userId}/contexts")]
        public async Task<ActionResult<List<UserContext>>> GetUserContexts(int userId, int limit = 50)
        {
            try
            {
                _logger.LogInformation($"Retrieving contexts for user {userId} with limit {limit}");

                if (limit <= 0 || limit > 100)
                {
                    limit = 50; // Default to 50, max 100
                }

                var userContexts = await _cosmosDbService.GetUserContextsAsync(userId, limit);

                return Ok(userContexts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving contexts for user {userId}");
                return StatusCode(500, "An error occurred while retrieving user contexts");
            }
        }

        /// <summary>
        /// Delete a specific user context
        /// </summary>
        /// <param name="contextId">The context ID to delete</param>
        /// <param name="userId">The user ID</param>
        /// <returns>Success status</returns>
        [HttpDelete("context/{contextId}/user/{userId}")]
        public async Task<ActionResult> DeleteUserContext(string contextId, int userId)
        {
            try
            {
                _logger.LogInformation($"Deleting context {contextId} for user {userId}");

                var deleted = await _cosmosDbService.DeleteUserContextAsync(contextId, userId);

                if (!deleted)
                {
                    return NotFound($"Context {contextId} not found for user {userId}");
                }

                return Ok(new { Message = $"Context {contextId} deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting context {contextId} for user {userId}");
                return StatusCode(500, "An error occurred while deleting user context");
            }
        }

        /// <summary>
        /// Search user contexts by type or tags
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="type">Context type to filter by (optional)</param>
        /// <param name="tag">Tag to filter by (optional)</param>
        /// <param name="limit">Maximum number of contexts to return (default: 20)</param>
        /// <returns>Filtered list of user contexts</returns>
        [HttpGet("user/{userId}/search")]
        public async Task<ActionResult<List<UserContext>>> SearchUserContexts(
            int userId, 
            string? type = null, 
            string? tag = null, 
            int limit = 20)
        {
            try
            {
                _logger.LogInformation($"Searching contexts for user {userId} with type: {type}, tag: {tag}");

                var allContexts = await _cosmosDbService.GetUserContextsAsync(userId, 100);

                var filteredContexts = allContexts.AsQueryable();

                if (!string.IsNullOrEmpty(type))
                {
                    filteredContexts = filteredContexts.Where(c => 
                        c.StructuredContext.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(tag))
                {
                    filteredContexts = filteredContexts.Where(c => 
                        c.StructuredContext.Tags.Any(t => t.Contains(tag, StringComparison.OrdinalIgnoreCase)));
                }

                var results = filteredContexts
                    .Take(limit)
                    .ToList();

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching contexts for user {userId}");
                return StatusCode(500, "An error occurred while searching user contexts");
            }
        }
    }
}