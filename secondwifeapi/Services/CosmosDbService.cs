using Microsoft.Azure.Cosmos;
using secondwifeapi.Models;
using Newtonsoft.Json;

namespace secondwifeapi.Services
{
    public interface ICosmosDbService
    {
        Task<UserContext> SaveUserContextAsync(UserContext userContext);
        Task<UserContext?> GetUserContextAsync(string contextId, int userId);
        Task<List<UserContext>> GetUserContextsAsync(int userId, int limit = 50);
        Task<bool> DeleteUserContextAsync(string contextId, int userId);
    }

    public class CosmosDbService : ICosmosDbService
    {
        private readonly Container _container;
        private readonly ILogger<CosmosDbService> _logger;

        public CosmosDbService(CosmosClient cosmosClient, IConfiguration configuration, ILogger<CosmosDbService> logger)
        {
            var databaseName = configuration["CosmosDB:DatabaseName"] ?? throw new ArgumentNullException("CosmosDB:DatabaseName");
            var containerName = configuration["CosmosDB:ContainerName"] ?? throw new ArgumentNullException("CosmosDB:ContainerName");
            
            _container = cosmosClient.GetContainer(databaseName, containerName);
            _logger = logger;
        }

        public async Task<UserContext> SaveUserContextAsync(UserContext userContext)
        {
            try
            {
                _logger.LogInformation($"Saving user context {userContext.ContextId} for user {userContext.UserId}");

                // Set the document ID and timestamps
                userContext.Id = $"{userContext.ContextId}_{userContext.UserId}";
                userContext.CreatedAt = DateTime.UtcNow;
                userContext.UpdatedAt = DateTime.UtcNow;

                // Use UserId as the partition key value since that's what you configured in Cosmos DB
                var response = await _container.CreateItemAsync(
                    userContext, 
                    new PartitionKey(userContext.UserId)
                );

                _logger.LogInformation($"Successfully saved user context {userContext.ContextId}");
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _logger.LogWarning($"User context {userContext.ContextId} already exists, updating instead");
                
                // Update existing document
                userContext.UpdatedAt = DateTime.UtcNow;
                var response = await _container.ReplaceItemAsync(
                    userContext,
                    userContext.Id,
                    new PartitionKey(userContext.UserId)
                );
                
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving user context {userContext.ContextId} for user {userContext.UserId}");
                throw;
            }
        }

        public async Task<UserContext?> GetUserContextAsync(string contextId, int userId)
        {
            try
            {
                _logger.LogInformation($"Retrieving user context {contextId} for user {userId}");

                var id = $"{contextId}_{userId}";

                var response = await _container.ReadItemAsync<UserContext>(
                    id,
                    new PartitionKey(userId)
                );

                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation($"User context {contextId} not found for user {userId}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving user context {contextId} for user {userId}");
                throw;
            }
        }

        public async Task<List<UserContext>> GetUserContextsAsync(int userId, int limit = 50)
        {
            try
            {
                _logger.LogInformation($"Retrieving user contexts for user {userId} with limit {limit}");

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.userId = @userId AND c.isActive = true ORDER BY c.createdAt DESC OFFSET 0 LIMIT @limit"
                )
                .WithParameter("@userId", userId)
                .WithParameter("@limit", limit);

                var results = new List<UserContext>();
                var iterator = _container.GetItemQueryIterator<UserContext>(
                    query,
                    requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) }
                );

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response.ToList());
                }

                _logger.LogInformation($"Retrieved {results.Count} user contexts for user {userId}");
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving user contexts for user {userId}");
                throw;
            }
        }

        public async Task<bool> DeleteUserContextAsync(string contextId, int userId)
        {
            try
            {
                _logger.LogInformation($"Deleting user context {contextId} for user {userId}");

                var id = $"{contextId}_{userId}";

                // Soft delete by marking as inactive
                var existingContext = await GetUserContextAsync(contextId, userId);
                if (existingContext == null)
                {
                    return false;
                }

                existingContext.IsActive = false;
                existingContext.UpdatedAt = DateTime.UtcNow;

                await _container.ReplaceItemAsync(
                    existingContext,
                    id,
                    new PartitionKey(userId)
                );

                _logger.LogInformation($"Successfully deleted user context {contextId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting user context {contextId} for user {userId}");
                throw;
            }
        }
    }
}