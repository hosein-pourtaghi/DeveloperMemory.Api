using Serilog;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using DeveloperMemory.Api.Abstractions;
using DeveloperMemory.Api.Services;
using DeveloperMemory.Api.Infrastructure.Configuration;
using DeveloperMemory.Api.Infrastructure.Middleware;
using DeveloperMemory.Infrastructure.DependencyInjection;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── OpenTelemetry ──
var otelEnabled = builder.Configuration.GetValue<bool>("OpenTelemetry:Enabled");
var otelServiceName = builder.Configuration.GetValue<string>("OpenTelemetry:ServiceName") ?? "DeveloperMemory.Api";
var otlpEndpoint = builder.Configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint");

if (otelEnabled)
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(otelServiceName))
        .WithTracing(tracing =>
        {
            tracing.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();
            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                tracing.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
            }
            else
            {
                tracing.AddConsoleExporter();
            }
        })
        .WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();
            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                metrics.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
            }
            else
            {
                metrics.AddConsoleExporter();
            }
        });

    builder.Logging.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(otelServiceName));
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            options.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
        }
        else
        {
            options.AddConsoleExporter();
        }
    });
}

// Configure Serilog
builder.Host.UseSerilog((context, services) =>
{
    services.ReadFrom.Configuration(context.Configuration);
});

// ── Infrastructure (EF Core, Repositories, Services) ──
builder.Services.AddDeveloperMemoryInfrastructure(builder.Configuration);

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
        Description = "Persistent AI memory and intelligence control plane.",
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

// Register existing services (file-based knowledge and profiles)
builder.Services.AddSingleton<ProfileService>();
builder.Services.AddSingleton<KnowledgeService>();
builder.Services.AddSingleton<RequestLogger>();

// Register the model gateway: FreeLlmApiClient is the current provider-specific implementation.
// To swap providers, change this registration to a different IModelGateway implementation.
builder.Services.AddHttpClient<FreeLlmApiClient>();
builder.Services.AddSingleton<IModelGateway>(sp => sp.GetRequiredService<FreeLlmApiClient>());

// Register the memory retriever: ContextRetrievalService orchestrates persistent memory
// and knowledge document retrieval behind the IMemoryRetriever abstraction.
// To change retrieval strategy (e.g., add vector search), replace this registration.
builder.Services.AddSingleton<IMemoryRetriever, ContextRetrievalService>();


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

// ── Database Migration ──
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DeveloperMemoryDbContext>();
    var useInMemory = app.Configuration.GetValue<bool>("UseInMemoryDatabase");

    if (!useInMemory)
    {
        try
        {
            await dbContext.Database.MigrateAsync();
            Log.Information("Database migration applied successfully.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Database migration failed. Application will continue with in-memory fallback.");
        }
    }
}

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
app.MapGet("/health", async (DeveloperMemoryDbContext dbContext) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync();
    return Results.Ok(new
    {
        Status = canConnect ? "Healthy" : "Degraded",
        Database = canConnect ? "Connected" : "Unavailable",
        Timestamp = DateTime.UtcNow
    });
});

app.Run();
