using Serilog;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using DeveloperMemory.Api.Abstractions;
using DeveloperMemory.Api.Infrastructure.Security;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using DeveloperMemory.Api.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication;
using DeveloperMemory.Api.Services;
using DeveloperMemory.Api.Infrastructure.Configuration;
using DeveloperMemory.Api.Infrastructure.Middleware;
using DeveloperMemory.Domain.Interfaces;
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
// If PostgreSQL is configured but unreachable at startup, fall back to in-memory.
var useInMemoryConfig = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");
if (!useInMemoryConfig)
{
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connStr))
    {
        Log.Warning("No 'DefaultConnection' configured. Falling back to in-memory database.");
        builder.Configuration["UseInMemoryDatabase"] = "true";
    }
    else
    {
        try
        {
            // Quick connectivity check before full service registration
            using var testConn = new Npgsql.NpgsqlConnection(connStr);
            testConn.Open();
            testConn.Close();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PostgreSQL unreachable. Falling back to in-memory database.");
            builder.Configuration["UseInMemoryDatabase"] = "true";
        }
    }
}
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
builder.Services.AddScoped<IMemoryRetriever, ContextRetrievalService>();



// ── Authentication (API Key via Bearer token) ──
builder.Services.Configure<ApiKeySettings>(builder.Configuration.GetSection("Authentication"));
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "DevelopmentOrApiKey";
        options.DefaultChallengeScheme = "DevelopmentOrApiKey";
    })
    .AddPolicyScheme("DevelopmentOrApiKey", "Development bypass or API key", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var settings = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiKeySettings>>().Value;
            var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
            var hasBearerToken = context.Request.Headers.TryGetValue("Authorization", out var authorization)
                && authorization.Any(value => value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase));

            return environment.IsDevelopment() && settings.DevelopmentBypass && !hasBearerToken
                ? "Development"
                : "ApiKey";
        };
    })
    .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>("Development", options => { })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", options => { });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<DeveloperMemory.Application.Contracts.ICurrentUser, HttpContextCurrentUser>();

// ── Security Audit Trail ──
// Persistent audit for PostgreSQL, in-memory for development/testing
if (!builder.Configuration.GetValue<bool>("UseInMemoryDatabase"))
{
    builder.Services.AddScoped<IAuditRepository, AuditRepository>();
    builder.Services.AddScoped<ISecurityAuditService, PersistentSecurityAuditService>();
}
else
{
    builder.Services.AddSingleton<ISecurityAuditService, InMemorySecurityAuditService>();
}

// ── Rate Limiting (per-identity partitioned by endpoint category) ──
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    // Single partitioned limiter: partitions by authenticated identity (userId or IP),
    // then applies per-endpoint-category limits based on the request path.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var partitionKey = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(partitionKey))
        {
            partitionKey = $"ip:{httpContext.Connection.RemoteIpAddress}";
        }

        var path = httpContext.Request.Path.Value ?? string.Empty;

        // Key management endpoints: 20/min per identity
        if (path.StartsWith("/api/ApiKey", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                $"km:{partitionKey}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        }

        // Expensive retrieval/query endpoints: 50/min per identity
        if (path.Contains("/query", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/retrieve", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/analyze", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/embedding", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                $"ex:{partitionKey}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 50,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        }

        // General endpoints: 200/min per identity
        return RateLimitPartition.GetFixedWindowLimiter(
            $"gen:{partitionKey}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

// Add CORS - environment-specific
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // No origins configured: block all cross-origin requests
            policy.DisallowCredentials()
                  .WithOrigins(Array.Empty<string>());
        }
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
            Log.Warning(ex, "Database migration failed. Application running with PostgreSQL configuration but database may be degraded.");
        }
    }
    else
    {
        // Ensure in-memory database is initialized
        dbContext.Database.EnsureCreated();
        Log.Information("Using in-memory database.");
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

app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseRateLimiter();
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

try
{
    app.Run();
}
catch (System.Exception ex)
{
    throw;
}
