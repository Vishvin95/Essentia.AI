# Fix for Azure Queue Message Deletion Issue

## Problem Description
The application was encountering `Azure.RequestFailedException: The specified message does not exist` errors when trying to delete messages from Azure Storage Queue. This was happening because of multiple deletion attempts on the same message.

## Root Cause Analysis

### Original Issue
The `ExpenseProcessingService.ProcessMessage` method had multiple code paths that could call `DeleteMessageAsync`:

1. **Early Returns**: JsonException handling, null message handling, invalid BlobSasUrl
2. **Success Path**: After successful processing
3. **Exception Path**: After failed processing

This created scenarios where a message could be deleted multiple times:
- If an exception occurred after an early return deletion
- If both success and exception paths were somehow executed
- If the message visibility timeout caused the message to be processed by multiple instances

### Code Locations
The problematic locations were:
```csharp
// Line ~95: JsonException handling
await queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt);

// Line ~102: Null message handling  
await queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt);

// Line ~112: Invalid BlobSasUrl handling
await queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt);

// Line ~173: Success path
await queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt);

// Line ~196: Exception path
await queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt);
```

## Solution Implementation

### 1. Safe Deletion Method
Created a `SafeDeleteMessageAsync` method that:
- Handles the specific "MessageNotFound" error gracefully
- Logs appropriate messages for debugging
- Returns success status to prevent multiple deletion attempts

```csharp
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
```

### 2. Message Deletion Tracking
Added a `messageDeleted` boolean flag to track deletion status and prevent duplicate deletions:

```csharp
private async Task ProcessMessage(string messageText, QueueClient queueClient, Azure.Storage.Queues.Models.QueueMessage queueMessage)
{
    ProcessExpenseMessage? expenseMessage = null;
    bool messageDeleted = false;
    
    // ... processing logic ...
    
    // Only delete if not already deleted
    if (!messageDeleted)
    {
        messageDeleted = await SafeDeleteMessageAsync(queueClient, queueMessage);
    }
}
```

### 3. Improved Error Handling
Enhanced the main processing loop to:
- Handle empty messages safely
- Log appropriate warnings and errors
- Prevent message deletion in case of processing exceptions (allowing for retry)

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Error processing message: {MessageId}. Message will be left in queue for retry or manual handling.", message.MessageId);
    // Don't delete the message here - let it be retried or handled manually
}
```

### 4. Webhook Error Handling
Added try-catch around webhook notifications to prevent webhook failures from affecting message processing:

```csharp
try
{
    await SendWebhookNotification(expenseMessage.WebhookUrl, errorData);
}
catch (Exception webhookEx)
{
    _logger.LogError(webhookEx, "Failed to send error webhook notification for JobId: {JobId}", expenseMessage.JobId);
}
```

## Benefits of the Fix

1. **Eliminates "MessageNotFound" Errors**: The safe deletion method handles this error gracefully
2. **Prevents Duplicate Deletions**: Message deletion tracking prevents multiple deletion attempts
3. **Improves Reliability**: Better error handling prevents cascading failures
4. **Enhanced Logging**: More detailed logging for troubleshooting
5. **Graceful Degradation**: Webhook failures don't affect core processing

## Testing Recommendations

To verify the fix works correctly:

1. **Start the Application**: Run `dotnet run` 
2. **Submit Multiple Messages**: Use the save-expense API to queue several messages
3. **Monitor Logs**: Check for any "MessageNotFound" errors
4. **Verify Processing**: Ensure all messages are processed successfully
5. **Test Error Scenarios**: Submit invalid messages to test error handling paths

## Monitoring Points

After deployment, monitor for:
- Reduction in "MessageNotFound" errors
- Successful message processing rates  
- Queue message count (should not accumulate due to deletion failures)
- Webhook delivery success rates

## Additional Improvements

Consider these future enhancements:
1. **Dead Letter Queue**: Implement a dead letter queue for repeatedly failing messages
2. **Message Retry Logic**: Implement exponential backoff for transient failures
3. **Health Checks**: Add health checks for queue processing status
4. **Metrics**: Add performance counters for message processing rates and errors