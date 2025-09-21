# User Context Processing API Documentation

This document describes the User Context Processing API that allows users to submit free-form text, processes it using Azure OpenAI to extract structured information, and stores it in Cosmos DB.

## Overview

The User Context API enables users to:
- Submit contextual information in natural language
- Automatically extract structured data using AI
- Store and retrieve user contexts from Cosmos DB
- Search and filter contexts by type, tags, or other criteria

## Base URL
```
http://localhost:5256/api/usercontext
```

## Endpoints

### 1. Process User Context
**POST** `/process-context`

Processes free-form user context text and extracts structured information using Azure OpenAI, then saves it to Cosmos DB.

#### Request Body
```json
{
  "userId": 101,
  "contextText": "I will be out of town for a week starting next Monday for a business trip to New York."
}
```

#### Response
```json
{
  "contextId": "CTX_20250921_A1B2C3D4",
  "message": "User context processed and saved successfully",
  "savedContext": {
    "id": "CTX_20250921_A1B2C3D4_101",
    "userId": 101,
    "contextId": "CTX_20250921_A1B2C3D4",
    "contextText": "I will be out of town for a week starting next Monday for a business trip to New York.",
    "structuredContext": {
      "type": "travel",
      "confidence": 0.9,
      "extractedData": {
        "purpose": "business trip",
        "duration": "1 week",
        "destination": "New York"
      },
      "tags": ["travel", "business", "trip"],
      "dateReferences": [
        {
          "originalText": "next Monday",
          "parsedDate": "2025-09-28T00:00:00Z",
          "dateType": "relative",
          "confidence": 0.8
        }
      ],
      "locationReferences": ["New York"],
      "peopleReferences": []
    },
    "createdAt": "2025-09-21T09:00:00Z",
    "updatedAt": "2025-09-21T09:00:00Z",
    "isActive": true,
    "partitionKey": "user_101"
  }
}
```

#### Example Usage
```powershell
$body = @{
    UserId = 1
    ContextText = "I am hosting a party on Sep 21 with John and Sarah."
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5256/api/usercontext/process-context" -Method POST -Body $body -ContentType "application/json"
```

---

### 2. Get Specific User Context
**GET** `/context/{contextId}/user/{userId}`

Retrieves a specific user context by context ID and user ID.

#### Parameters
- `contextId` (string) - The context ID to retrieve
- `userId` (integer) - The user ID

#### Response
Returns the full UserContext object if found, 404 if not found.

#### Example Usage
```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5256/api/usercontext/context/CTX_20250921_A1B2C3D4/user/101" -Method GET
```

---

### 3. Get All User Contexts
**GET** `/user/{userId}/contexts?limit={limit}`

Retrieves all contexts for a specific user, ordered by creation date (newest first).

#### Parameters
- `userId` (integer) - The user ID
- `limit` (integer, optional) - Maximum number of contexts to return (default: 50, max: 100)

#### Response
Returns an array of UserContext objects for the user.

#### Example Usage
```powershell
# Get all contexts for user 1
$response = Invoke-RestMethod -Uri "http://localhost:5256/api/usercontext/user/1/contexts" -Method GET

# Get latest 10 contexts
$response = Invoke-RestMethod -Uri "http://localhost:5256/api/usercontext/user/1/contexts?limit=10" -Method GET
```

---

### 4. Search User Contexts
**GET** `/user/{userId}/search?type={type}&tag={tag}&limit={limit}`

Search and filter user contexts by type, tags, or other criteria.

#### Parameters
- `userId` (integer) - The user ID
- `type` (string, optional) - Filter by context type (e.g., "travel", "event", "personal")
- `tag` (string, optional) - Filter by tag (partial match)
- `limit` (integer, optional) - Maximum number of results (default: 20)

#### Response
Returns an array of matching UserContext objects.

#### Example Usage
```powershell
# Search for travel contexts
$response = Invoke-RestMethod -Uri "http://localhost:5256/api/usercontext/user/1/search?type=travel" -Method GET

# Search for contexts with "party" tag
$response = Invoke-RestMethod -Uri "http://localhost:5256/api/usercontext/user/1/search?tag=party" -Method GET

# Search for event contexts with limit
$response = Invoke-RestMethod -Uri "http://localhost:5256/api/usercontext/user/1/search?type=event&limit=5" -Method GET
```

---

### 5. Delete User Context
**DELETE** `/context/{contextId}/user/{userId}`

Soft deletes a user context by marking it as inactive.

#### Parameters
- `contextId` (string) - The context ID to delete
- `userId` (integer) - The user ID

#### Response
```json
{
  "message": "Context CTX_20250921_A1B2C3D4 deleted successfully"
}
```

#### Example Usage
```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5256/api/usercontext/context/CTX_20250921_A1B2C3D4/user/1" -Method DELETE
```

---

## Data Models

### UserContext
The main document stored in Cosmos DB:

```json
{
  "id": "CTX_20250921_A1B2C3D4_101",
  "userId": 101,
  "contextId": "CTX_20250921_A1B2C3D4",
  "contextText": "Original user input text",
  "structuredContext": { ... },
  "createdAt": "2025-09-21T09:00:00Z",
  "updatedAt": "2025-09-21T09:00:00Z",
  "isActive": true,
  "partitionKey": "user_101"
}
```

### StructuredContext
AI-extracted structured information:

```json
{
  "type": "travel",
  "confidence": 0.9,
  "extractedData": {
    "purpose": "business trip",
    "duration": "1 week"
  },
  "tags": ["travel", "business"],
  "dateReferences": [ ... ],
  "locationReferences": ["New York"],
  "peopleReferences": ["John", "Sarah"]
}
```

### DateReference
Extracted date information:

```json
{
  "originalText": "next Monday",
  "parsedDate": "2025-09-28T00:00:00Z",
  "dateType": "relative",
  "confidence": 0.8
}
```

---

## Context Types

The AI system automatically categorizes contexts into types such as:

- **travel** - Travel plans, trips, vacations
- **event** - Parties, meetings, appointments
- **personal** - Health, family, personal matters
- **work** - Business, professional activities
- **social** - Social gatherings, relationships
- **financial** - Money-related contexts
- **health** - Medical appointments, wellness
- **unknown** - Unable to categorize

---

## Configuration

### Required Settings in appsettings.json

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-openai.openai.azure.com/",
    "ApiKey": "your-openai-api-key",
    "DeploymentName": "gpt-4-deployment-name"
  },
  "CosmosDB": {
    "EndpointUri": "https://your-cosmosdb.documents.azure.com:443/",
    "PrimaryKey": "your-cosmos-db-primary-key",
    "DatabaseName": "UserContextDB",
    "ContainerName": "UserContexts"
  }
}
```

### Required NuGet Packages

```xml
<PackageReference Include="Azure.AI.OpenAI" Version="1.0.0-beta.17" />
<PackageReference Include="Microsoft.Azure.Cosmos" Version="3.39.1" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

---

## Error Responses

All endpoints return appropriate HTTP status codes:

- `200 OK` - Success
- `400 Bad Request` - Invalid input (empty context text, invalid user ID)
- `404 Not Found` - Context or user not found
- `500 Internal Server Error` - Processing error

### Error Response Format
```json
{
  "error": "Context text is required"
}
```

---

## Use Cases

### 1. Personal Assistant Integration
- Users can input natural language contexts
- System automatically categorizes and structures the information
- Easy retrieval of relevant contexts for planning and reminders

### 2. Expense Context Enhancement
- Link user contexts to expense patterns
- "I'm traveling to NYC" context can inform expense categorization
- Better expense predictions based on user context

### 3. Smart Recommendations
- Use context history to provide intelligent suggestions
- Predict upcoming expenses based on context patterns
- Personalized user experience

### 4. Analytics and Insights
- Analyze user behavior patterns from contexts
- Generate insights about user lifestyle and preferences
- Improve service offerings based on context analysis

---

## Testing

Run the comprehensive test script:
```powershell
.\test_user_context.ps1
```

This script tests:
- Context processing with various types of input
- Context retrieval and search functionality
- Error handling scenarios
- CRUD operations

---

## Security Considerations

1. **Data Privacy**: User contexts may contain sensitive information
2. **Access Control**: Ensure users can only access their own contexts
3. **Data Retention**: Consider implementing data retention policies
4. **Encryption**: Cosmos DB handles encryption at rest
5. **API Authentication**: Consider adding authentication middleware

---

## Performance Considerations

1. **Cosmos DB Partitioning**: Uses `user_{userId}` as partition key for optimal performance
2. **Caching**: Consider adding Redis caching for frequently accessed contexts
3. **Rate Limiting**: Implement rate limiting for Azure OpenAI calls
4. **Batch Processing**: For high-volume scenarios, consider batch processing contexts