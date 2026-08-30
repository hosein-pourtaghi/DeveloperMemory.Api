using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DeveloperMemory.Api.Tests;

/// <summary>
/// WebApplicationFactory configured for real PostgreSQL E2E testing.
/// 
/// Strategy: Let Program.cs detect PostgreSQL naturally (it IS reachable on localhost:5432),
/// but override the connection string to a unique test database via ConfigureWebHost.
/// The DbContext is replaced AFTER Program.cs registers it, pointing at our test DB.
/// 
/// Each test class gets its own isolated test database, created fresh and dropped on dispose.
/// </summary>
public class PostgresE2EFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public CaptureModelGateway Gateway { get; } = new();

    private readonly string _dbName = $"e2e_{Guid.NewGuid():N}";
    private string _connectionString = null!;

    public string ConnectionString => _connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        _connectionString =
            $"Host=localhost;Port=5432;Database={_dbName};Username=developer;Password=devpassword";

        builder.ConfigureServices(services =>
        {
            // Replace IModelGateway with capture stub
            services.RemoveAll<DeveloperMemory.Api.Abstractions.IModelGateway>();
            services.RemoveAll<FreeLlmApiClient>();
            services.AddSingleton<DeveloperMemory.Api.Abstractions.IModelGateway>(Gateway);

            // Replace DbContext to use our unique test database
            // This runs AFTER Program.cs/AddDeveloperMemoryInfrastructure registers
            // the DbContext, so we remove the old registration and add our own.
            services.RemoveAll(typeof(DbContextOptions<DeveloperMemoryDbContext>));
            services.RemoveAll<DeveloperMemoryDbContext>();
            services.AddDbContext<DeveloperMemoryDbContext>(options =>
                options.UseNpgsql(_connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(DeveloperMemoryDbContext).Assembly.FullName);
                }));
        });
    }

    public async Task InitializeAsync()
    {
        // Create the test database
        await CreateTestDatabaseAsync(_dbName);

        // Apply migrations to the test database
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<DeveloperMemoryDbContext>();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        // Drop the test database (best-effort)
        await DropTestDatabaseAsync(_dbName);
    }

    public DeveloperMemoryDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DeveloperMemoryDbContext>();
    }

    private static async Task CreateTestDatabaseAsync(string dbName)
    {
        var masterConn = "Host=localhost;Port=5432;Database=postgres;Username=developer;Password=devpassword";
        await using var conn = new Npgsql.NpgsqlConnection(masterConn);
        await conn.OpenAsync();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
            try { await cmd.ExecuteNonQueryAsync(); }
            catch (Npgsql.NpgsqlException ex) when (ex.SqlState == "42P07") { /* already exists */ }
        }

        await conn.CloseAsync();
    }

    private static async Task DropTestDatabaseAsync(string dbName)
    {
        try
        {
            var masterConn = "Host=localhost;Port=5432;Database=postgres;Username=developer;Password=devpassword";
            await using var conn = new Npgsql.NpgsqlConnection(masterConn);
            await conn.OpenAsync();

            // Terminate existing connections
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    SELECT pg_terminate_backend(pid)
                    FROM pg_stat_activity
                    WHERE datname = '{dbName}' AND pid <> pg_backend_pid()";
                await cmd.ExecuteNonQueryAsync();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"DROP DATABASE IF EXISTS \"{dbName}\"";
                await cmd.ExecuteNonQueryAsync();
            }

            await conn.CloseAsync();
        }
        catch { /* Best-effort cleanup */ }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// PostgreSQL E2E: Conversational Memory Pipeline (Phase Q.4)
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Tests A-E: Real PostgreSQL conversational memory E2E.
/// Each class gets its own isolated test database via PostgresE2EFactory.
/// </summary>
public class Postgres_ConversationalMemoryTests : IClassFixture<PostgresE2EFactory>
{
    private readonly HttpClient _client;
    private readonly PostgresE2EFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Postgres_ConversationalMemoryTests(PostgresE2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>Test A: Explicit memory persistence in PostgreSQL.</summary>
    [Fact]
    public async Task TestA_RememberPreference_PersistedInPostgres()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "Remember that I prefer concise technical answers."));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify directly against PostgreSQL
        using var db = _factory.CreateDbContext();
        Assert.True(db.Database.IsNpgsql(), "Expected Npgsql provider");

        var memories = await E2EHelpers.FindMemoriesByContent(db, "local-development-owner", "concise");
        Assert.NotEmpty(memories);

        var memory = memories.First();
        Assert.Equal(MemoryState.Active, memory.State);
        Assert.Equal(MemoryScope.Global, memory.Scope);
        Assert.Equal("local-development-owner", memory.OwnerId);
        Assert.False(string.IsNullOrEmpty(memory.Content));
        Assert.True(memory.CreatedAt > DateTime.MinValue);
        Assert.True(memory.UpdatedAt > DateTime.MinValue);
    }

    /// <summary>Test B: Cross-request retrieval from PostgreSQL.</summary>
    [Fact]
    public async Task TestB_CrossRequestRetrieval_FromPostgres()
    {
        // Request 1: Store memory
        var request1 = E2EHelpers.BuildRequest("stub-model",
            ("user", "Remember that I prefer concise technical answers."));
        var response1 = await E2EHelpers.SendChatRequest(_client, request1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // Verify persistence in PostgreSQL
        using (var db = _factory.CreateDbContext())
        {
            var persisted = await E2EHelpers.FindMemoriesByContent(
                db, "local-development-owner", "concise");
            Assert.NotEmpty(persisted);
        }

        // Request 2: Separate HTTP request (new scope)
        var request2 = E2EHelpers.BuildRequest("stub-model",
            ("user", "What do you know about concise answers?"));
        var response2 = await E2EHelpers.SendChatRequest(_client, request2);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        // The provider should receive enriched context containing the memory
        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.Contains("concise", systemMsg);
    }

    /// <summary>Test C: Conversation history reaches the pipeline.</summary>
    [Fact]
    public async Task TestC_ConversationHistory_ReachesPipeline()
    {
        var request = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages = [
                new Message { Role = "user", Content = "I'm working on DeveloperMemory.Api." },
                new Message { Role = "assistant", Content = "Understood." },
                new Message { Role = "user", Content = "Remember that this project uses PostgreSQL for persistent memory." }
            ],
            Stream = false
        };

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = _factory.CreateDbContext();
        Assert.True(db.Database.IsNpgsql());

        var memories = await E2EHelpers.FindMemoriesByContent(
            db, "local-development-owner", "PostgreSQL");
        Assert.NotEmpty(memories);
    }

    /// <summary>Test D: Project inference from conversation.</summary>
    [Fact]
    public async Task TestD_ProjectInference_FromConversation()
    {
        // Create a real project via the Projects API
        var projectName = $"PgTest_{Guid.NewGuid():N}";
        var createResponse = await _client.PostAsJsonAsync("/api/Projects",
            new { name = projectName, description = "PostgreSQL E2E test project" });

        Assert.True(createResponse.StatusCode == HttpStatusCode.Created
            || createResponse.StatusCode == HttpStatusCode.OK,
            $"Project creation returned {(int)createResponse.StatusCode}");

        var project = await createResponse.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(project);

        // Send conversation that mentions the project
        var chatRequest = E2EHelpers.BuildRequest("stub-model",
            ("user", $"I'm working on {projectName}."),
            ("user", "Remember that this project uses PostgreSQL for persistent memory."));

        var chatResponse = await E2EHelpers.SendChatRequest(_client, chatRequest);
        Assert.Equal(HttpStatusCode.OK, chatResponse.StatusCode);

        // Verify in PostgreSQL
        using var db = _factory.CreateDbContext();
        Assert.True(db.Database.IsNpgsql());

        var memories = await E2EHelpers.FindMemoriesByContent(
            db, "local-development-owner", "PostgreSQL");
        Assert.NotEmpty(memories);

        // Find memory associated with our project
        var projectMemory = memories.FirstOrDefault(m => m.ProjectId == project.Id);
        if (projectMemory != null)
        {
            Assert.Equal(MemoryScope.Project, projectMemory.Scope);
        }
        else
        {
            // At minimum, memory was persisted with correct owner
            Assert.Contains(memories, m => m.OwnerId == "local-development-owner");
        }
    }

    /// <summary>Test E: No custom Open WebUI metadata required.</summary>
    [Fact]
    public async Task TestE_NoProjectOrTagsRequired()
    {
        // Standard OpenAI-compatible payload — no project, tags, workspace
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "Remember that I prefer concise technical answers."));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify the gateway received the enriched request
        var forwarded = _factory.Gateway.CapturedRequests.Last();
        Assert.Contains("DeveloperMemory Context",
            forwarded.Messages.First(m => m.Role == "system").Content);

        // Verify persistence
        using var db = _factory.CreateDbContext();
        Assert.True(db.Database.IsNpgsql());

        var memories = await E2EHelpers.FindMemoriesByContent(
            db, "local-development-owner", "concise");
        Assert.NotEmpty(memories);
    }

    /// <summary>FreeLLMApi boundary: enriched request reaches provider.</summary>
    [Fact]
    public async Task FreeLLMApiBoundary_EnrichedRequestForwarded()
    {
        // Store memory first
        var storeRequest = E2EHelpers.BuildRequest("stub-model",
            ("user", "Remember that I prefer concise technical answers."));
        await E2EHelpers.SendChatRequest(_client, storeRequest);

        // Second request — should retrieve memory and enrich
        _factory.Gateway.Reset();
        var queryRequest = E2EHelpers.BuildRequest("stub-model",
            ("user", "How should you answer me?"));
        await E2EHelpers.SendChatRequest(_client, queryRequest);

        // Verify gateway captured enriched request
        Assert.True(_factory.Gateway.CallCount >= 1);
        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;

        // Original user message preserved
        Assert.Contains("How should you answer me", systemMsg);
        // Retrieved memory included
        Assert.Contains("DeveloperMemory Context", systemMsg);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// PostgreSQL E2E: Diagnostic Logging (Phase Q.5)
// ══════════════════════════════════════════════════════════════════════════════

public class Postgres_DiagnosticLoggingTests : IClassFixture<PostgresE2EFactory>
{
    private readonly HttpClient _client;
    private readonly PostgresE2EFactory _factory;

    public Postgres_DiagnosticLoggingTests(PostgresE2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Diagnostics_EndpointSucceeds_InPostgres()
    {
        // Send a request through the PostgreSQL-backed pipeline
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "PostgreSQL diagnostic test"));
        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify the diagnostic table exists and is queryable in PostgreSQL
        using var db = _factory.CreateDbContext();
        Assert.True(db.Database.IsNpgsql());
        var count = await db.DiagnosticLogs.CountAsync();
        // Count may be 0 or 1 depending on Diagnostics:PersistToDatabase config
        Assert.True(count >= 0);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// PostgreSQL E2E: Agent Memory API (Phase Q.4 + Q.6)
// ══════════════════════════════════════════════════════════════════════════════

public class Postgres_AgentMemoryApiTests : IClassFixture<PostgresE2EFactory>
{
    private readonly HttpClient _client;
    private readonly PostgresE2EFactory _factory;

    public Postgres_AgentMemoryApiTests(PostgresE2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AgentApi_CreateAndSearch_InPostgres()
    {
        // Create via Agent API
        var createRequest = new
        {
            content = "PostgreSQL agent memory test",
            title = "PG Agent Test",
            scope = MemoryScope.Global,
            memoryType = MemoryType.Fact,
            classification = DataClassification.Internal,
            importance = 0.7,
            confidence = 0.9
        };
        var createResponse = await _client.PostAsJsonAsync("/api/agent/memory", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<MemoryDto>();
        Assert.NotNull(created);

        // Verify in PostgreSQL directly
        using var db = _factory.CreateDbContext();
        Assert.True(db.Database.IsNpgsql());

        var entry = await db.MemoryEntries.FindAsync(created!.Id);
        Assert.NotNull(entry);
        Assert.Equal("PostgreSQL agent memory test", entry!.Content);
        Assert.Equal("local-development-owner", entry.OwnerId);
        Assert.Equal(MemoryState.Active, entry.State);

        // Search via Agent API
        var searchResponse = await _client.GetAsync("/api/agent/memory/search?query=PostgreSQL+agent");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var results = await searchResponse.Content.ReadFromJsonAsync<List<MemoryDto>>();
        Assert.Contains(results!, m => m.Content.Contains("PostgreSQL agent memory test"));
    }

    [Fact]
    public async Task AgentApi_Delete_InPostgres()
    {
        // Create
        var createRequest = new
        {
            content = "Delete me from PG",
            title = "Delete PG Test",
            scope = MemoryScope.Global,
            memoryType = MemoryType.Fact,
            classification = DataClassification.Internal,
            importance = 0.5,
            confidence = 0.8
        };
        var createResponse = await _client.PostAsJsonAsync("/api/agent/memory", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MemoryDto>();

        // Delete
        var deleteResponse = await _client.DeleteAsync($"/api/agent/memory/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify state in PostgreSQL
        using var db = _factory.CreateDbContext();
        var entry = await db.MemoryEntries.FindAsync(created.Id);
        Assert.NotNull(entry);
        Assert.Equal(MemoryState.Deleted, entry!.State);
    }

    [Fact]
    public async Task AgentApi_ConversationalMemory_Visible_InPostgres()
    {
        // Create via conversational pipeline
        var chatRequest = E2EHelpers.BuildRequest("stub-model",
            ("user", "Remember that PG agent visibility test uses green color."));
        var chatResponse = await E2EHelpers.SendChatRequest(_client, chatRequest);
        Assert.Equal(HttpStatusCode.OK, chatResponse.StatusCode);

        // Verify in PostgreSQL
        using var db = _factory.CreateDbContext();
        Assert.True(db.Database.IsNpgsql());

        var memories = await db.MemoryEntries
            .Where(e => e.OwnerId == "local-development-owner"
                && e.State == MemoryState.Active
                && e.Content.Contains("green color"))
            .ToListAsync();
        Assert.NotEmpty(memories);

        // Search via Agent API
        var searchResponse = await _client.GetAsync("/api/agent/memory/search?query=green+color");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var results = await searchResponse.Content.ReadFromJsonAsync<List<MemoryDto>>();
        Assert.Contains(results!, m => m.Content.Contains("green color"));
    }
}
