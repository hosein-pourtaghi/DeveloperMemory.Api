using Serilog;
using DeveloperMemory.Api.Services;
using DeveloperMemory.Api.Infrastructure.Configuration;
using DeveloperMemory.Api.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services) =>
{
    services.ReadFrom.Configuration(context.Configuration);
});

// Add services to the container
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "DeveloperMemory API",
        Version = "v1",
        Description = "API for managing developer knowledge and profiles",
        Contact = new Microsoft.OpenApi.OpenApiContact
        {
            Name = "DeveloperMemory",
            Email = "support@developermemory.com"
        }
    });

    // Enable XML comments if generated
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Add JWT Bearer token support (Swashbuckle 7.x / .NET 10 style)
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    
});

// Configure AppSettings
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
builder.Services.Configure<FreeLlmApiSettings>(builder.Configuration.GetSection("FreeLlmApi"));
builder.Services.Configure<PathSettings>(builder.Configuration.GetSection("Paths"));

// Register services
builder.Services.AddSingleton<ProfileService>();
builder.Services.AddSingleton<KnowledgeService>();
builder.Services.AddSingleton<PromptBuilder>();
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

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DeveloperMemory API v1");
        c.RoutePrefix = "swagger"; // Serve Swagger UI at /swagger
        c.DocumentTitle = "DeveloperMemory API Documentation";
        c.DefaultModelsExpandDepth(-1); // Hide models by default
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Load documents on startup
var knowledgeService = app.Services.GetRequiredService<KnowledgeService>();
await knowledgeService.LoadDocumentsAsync();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

app.Run();
