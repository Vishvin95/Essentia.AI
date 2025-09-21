using Microsoft.EntityFrameworkCore;
using Azure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Entity Framework
builder.Services.AddDbContext<secondwifeapi.Data.ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add HttpClient for webhook notifications
builder.Services.AddHttpClient();

// Register expense extraction service
// For production with Azure OpenAI:
builder.Services.AddHttpClient<secondwifeapi.Services.IExpenseExtractionService, secondwifeapi.Services.AzureOpenAIExtractionService>();
// For testing with mock service:
// builder.Services.AddScoped<secondwifeapi.Services.IExpenseExtractionService, secondwifeapi.Services.MockExpenseExtractionService>();

// Register Cosmos DB services
builder.Services.AddSingleton(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var endpointUri = configuration["CosmosDB:EndpointUri"] ?? throw new InvalidOperationException("CosmosDB:EndpointUri not configured");
    var primaryKey = configuration["CosmosDB:PrimaryKey"] ?? throw new InvalidOperationException("CosmosDB:PrimaryKey not configured");
    return new Microsoft.Azure.Cosmos.CosmosClient(endpointUri, primaryKey);
});

// Register Azure OpenAI client
builder.Services.AddSingleton(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var endpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
    var apiKey = configuration["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");
    return new Azure.AI.OpenAI.OpenAIClient(new Uri(endpoint), new Azure.AzureKeyCredential(apiKey));
});

// Register context processing services
builder.Services.AddScoped<secondwifeapi.Services.IContextExtractionService, secondwifeapi.Services.ContextExtractionService>();
builder.Services.AddScoped<secondwifeapi.Services.ICosmosDbService, secondwifeapi.Services.CosmosDbService>();

// Register background service for expense processing (temporarily disabled for testing)
builder.Services.AddHostedService<secondwifeapi.Services.ExpenseProcessingService>();

var app = builder.Build();

// Automatically create/migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<secondwifeapi.Data.ApplicationDbContext>();
    try
    {
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

// Enable Swagger UI for all environments
app.UseSwagger();
app.UseSwaggerUI(c =>
{
	c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
	c.RoutePrefix = string.Empty; // Serve Swagger UI at application root
});

app.MapControllers();

app.Run();
