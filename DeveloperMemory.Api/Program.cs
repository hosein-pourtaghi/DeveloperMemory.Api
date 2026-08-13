using Serilog;
using Microsoft.OpenApi.Models;
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Developer Memory Gateway", Version = "v1" });
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
    app.UseSwaggerUI();
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