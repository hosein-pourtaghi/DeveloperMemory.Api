using Serilog;
using DeveloperMemory.Api.Services;
using DeveloperMemory.Api.Infrastructure.Configuration;
using DeveloperMemory.Api.Infrastructure.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services) =>
{
    services.ReadFrom.Configuration(context.Configuration);
});

// Add services to the container
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .SelectMany(e => e.Value!.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            var errorDetail = errors.Count > 0 ? string.Join("; ", errors) : "Invalid request body";

            var result = new Microsoft.AspNetCore.Mvc.ObjectResult(new
            {
                error = new
                {
                    message = errorDetail,
                    type = "invalid_request_error",
                    code = "bad_request",
                    param = (string?)null
                }
            });
            result.StatusCode = 400;
            result.ContentTypes.Add("application/json");
            return result;
        };
    });

// Add Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "DeveloperMemory API",
        Version = "v1",
        Description = "OpenAI-compatible Developer Memory Gateway.",
        Contact = new Microsoft.OpenApi.OpenApiContact
        {
            Name = "DeveloperMemory",
            Email = "support@developermemory.com"
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Configure strongly-typed settings
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
builder.Services.Configure<ModelSelectionSettings>(builder.Configuration.GetSection("AppSettings:ModelSelection"));

// Register services
builder.Services.AddSingleton<ProfileService>();
builder.Services.AddSingleton<KnowledgeService>();
builder.Services.AddSingleton<PromptBuilder>();
builder.Services.AddSingleton<RequestLogger>();
builder.Services.AddHttpClient<FreeLlmApiClient>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Diagnostic: log incoming request bodies for /v1/*
app.UseMiddleware<RequestLoggingMiddleware>();

// Global exception handler
app.UseMiddleware<GlobalExceptionMiddleware>();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DeveloperMemory API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "DeveloperMemory API Documentation";
        c.DefaultModelsExpandDepth(-1);
    });
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Load documents on startup
var knowledgeService = app.Services.GetRequiredService<KnowledgeService>();
await knowledgeService.LoadDocumentsAsync();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

app.Run();
