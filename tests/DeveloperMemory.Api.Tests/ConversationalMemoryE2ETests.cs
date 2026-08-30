using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DeveloperMemory.Api.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// Test infrastructure: Gateway stub, WebApplicationFactory, helpers
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Capture-only IModelGateway stub used in E2E tests.
/// Records every request forwarded to the provider for assertion.
/// </summary>
public class CaptureModelGateway : DeveloperMemory.Api.Abstractions.IModelGateway
{
    public bool IsConfigured { get; set; } = true;
    public List<OpenAIChatCompletionRequest> CapturedRequests { get; } = [];
    public List<OpenAIChatCompletionResponse> ResponsesToSend { get; set; } =
        [new() { Id = "chatcmpl-e2e", Model = "stub-model", Choices = [new() { Index = 0, Message = new() { Role = "assistant", Content = "stub response" }, FinishReason = "stop" }], Usage = new() { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 } }];

    public int CallCount { get; private set; }

    public Task<OpenAIChatCompletionResponse> SendCompletionAsync(OpenAIChatCompletionRequest request, CancellationToken ct = default)
    {
        CallCount++;
        CapturedRequests.Add(request);
        var idx = Math.Min(CallCount - 1, ResponsesToSend.Count - 1);
        return Task.FromResult(ResponsesToSend[idx]);
    }

    public Task<Stream> SendStreamingCompletionAsync(OpenAIChatCompletionRequest request, CancellationToken ct = default)
    {
        CallCount++;
        CapturedRequests.Add(request);
        var json = "{\"id\":\"chatcmpl-e2e\",\"object\":\"chat.completion.chunk\",\"model\":\"stub-model\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"hello\"},\"finish_reason\":null}]}\ndata: [DONE]\n";
        return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(json)));
    }

    public Task<List<string>> GetModelsAsync(CancellationToken ct = default) => Task.FromResult(new List<string> { "stub-model" });
    public Task<DeveloperMemory.Api.Models.OpenAIModel?> GetModelAsync(string modelId, CancellationToken ct = default) => Task.FromResult<DeveloperMemory.Api.Models.OpenAIModel?>(new() { Id = modelId });
    public string ResolveModel(string? requestedModel) => requestedModel ?? "stub-model";
}

/// <summary>Noop diagnostic log repository for E2E tests (satisfies middleware DI requirement).</summary>
public class NoopDiagnosticLogRepository : IDiagnosticLogRepository
{
    public List<DiagnosticLogEntry> Entries { get; } = [];
    public Task TryLogAsync(DiagnosticLogEntry entry, CancellationToken ct = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
    public Task TryLogBatchAsync(IReadOnlyList<DiagnosticLogEntry> entries, CancellationToken ct = default)
    {
        Entries.AddRange(entries);
        return Task.CompletedTask;
    }
}

/// <summary>
/// WebApplicationFactory that wires up in-memory DB + capture gateway for E2E tests.
/// Overrides configuration BEFORE Program.cs runs so only InMemory is registered.
/// Uses Development environment so auth bypass is active (no API key required).
/// </summary>
public class E2EFactory : WebApplicationFactory<Program>
{
    public CaptureModelGateway Gateway { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            // Force InMemory DB so Program.cs never tries Npgsql.
            // Add AFTER existing sources so these take highest priority.
            var inMemorySettings = new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["AppSettings:FreeLlmApi:BaseUrl"] = "http://localhost:9999/v1",
                ["AppSettings:FreeLlmApi:DefaultModel"] = "stub-model",
                ["AppSettings:ModelSelection:AutoSelectModel"] = "false",
                ["Authentication:DevelopmentBypass"] = "true",
                ["Authentication:DevelopmentOwnerId"] = "local-development-owner",
                ["Authentication:DevelopmentOwnerDisplayName"] = "Local Development Owner",
                ["Diagnostics:PersistToDatabase"] = "false",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:DeveloperMemory"] = "Information",
            };
            config.AddInMemoryCollection(inMemorySettings);
        });

        builder.ConfigureServices(services =>
        {
            // Replace IModelGateway with capture stub
            services.RemoveAll<DeveloperMemory.Api.Abstractions.IModelGateway>();
            services.RemoveAll<FreeLlmApiClient>();
            services.AddSingleton<DeveloperMemory.Api.Abstractions.IModelGateway>(Gateway);

            // Replace scoped IDiagnosticLogRepository with singleton noop.
            // DiagnosticLoggingMiddleware is resolved from root provider and needs
            // IDiagnosticLogRepository at construction time — scoped registration fails.
            services.RemoveAll<IDiagnosticLogRepository>();
            services.AddSingleton<IDiagnosticLogRepository>(new NoopDiagnosticLogRepository());
        });
    }

    /// <summary>Creates a scoped DbContext for direct DB inspection.</summary>
    public DeveloperMemoryDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DeveloperMemoryDbContext>();
    }
}

/// <summary>
/// Helpers for E2E tests.
/// </summary>
public static class E2EHelpers
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static StringContent ToJsonContent(object obj) =>
        new(JsonSerializer.Serialize(obj, JsonOpts), Encoding.UTF8, "application/json");

    public static OpenAIChatCompletionRequest BuildRequest(
        string? model = null,
        params (string role, string content)[] messages)
    {
        return new OpenAIChatCompletionRequest
        {
            Model = model,
            Messages = messages.Select(m => new Message { Role = m.role, Content = m.content }).ToList(),
            Stream = false
        };
    }

    /// <summary>Sends a chat completion request and returns the response.</summary>
    public static async Task<HttpResponseMessage> SendChatRequest(
        HttpClient client,
        OpenAIChatCompletionRequest request)
    {
        var json = JsonSerializer.Serialize(request, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PostAsync("/v1/chat/completions", content);
    }

    /// <summary>Verifies a memory exists in the DB with the given content substring.</summary>
    public static async Task<List<MemoryEntry>> FindMemoriesByContent(
        DeveloperMemoryDbContext db, string ownerId, string contentSubstring)
    {
        return await db.MemoryEntries
            .Where(e => e.OwnerId == ownerId && e.State != MemoryState.Deleted)
            .Where(e => e.Content.Contains(contentSubstring))
            .ToListAsync();
    }

    /// <summary>Verifies all active memories for a given owner.</summary>
    public static async Task<List<MemoryEntry>> GetAllActiveMemories(
        DeveloperMemoryDbContext db, string ownerId)
    {
        return await db.MemoryEntries
            .Where(e => e.OwnerId == ownerId && e.State == MemoryState.Active)
            .ToListAsync();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// RULE 2: OpenAI-compatible request processing
// ══════════════════════════════════════════════════════════════════════════════

public class E2E_OpenAiRequestTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;

    public E2E_OpenAiRequestTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostChatCompletions_WithStandardOpenAiPayload_ReturnsSuccess()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "I'm working on DeveloperMemory.Api."));

        var response = await E2EHelpers.SendChatRequest(_client, request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("chatcmpl", body);
    }

    [Fact]
    public async Task PostChatCompletions_WithoutProjectIdTagWorkspace_ReturnsSuccess()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "Remember that I prefer concise technical answers."));

        var response = await E2EHelpers.SendChatRequest(_client, request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostChatCompletions_EmptyMessages_Returns400()
    {
        var request = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages = []
        };

        var response = await E2EHelpers.SendChatRequest(_client, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostChatCompletions_NullBody_Returns400()
    {
        var response = await _client.PostAsync("/v1/chat/completions",
            new StringContent("null", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostChatCompletions_GatewayReceivesEnrichedRequest()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("system", "You are helpful."),
            ("user", "What is 2+2?"));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(1, _factory.Gateway.CallCount);
        var forwarded = _factory.Gateway.CapturedRequests[0];
        Assert.Contains("DeveloperMemory Context",
            forwarded.Messages.First(m => m.Role == "system").Content);
    }

    [Fact]
    public async Task PostChatCompletions_NoSystemMessage_ControllerAddsOne()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "Hello"));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests[0];
        var sysMsg = forwarded.Messages.FirstOrDefault(m => m.Role == "system");
        Assert.NotNull(sysMsg);
        Assert.Contains("DeveloperMemory Context", sysMsg.Content);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// RULE 3 + 4 + 7: Conversation history propagation + Memory persistence
// ══════════════════════════════════════════════════════════════════════════════

public class E2E_MemoryIngestionTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;

    public E2E_MemoryIngestionTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RememberExplicitPreference_MemoryPersistedToDb()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "Remember that I prefer concise technical answers."));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = _factory.CreateDbContext();
        var memories = await E2EHelpers.FindMemoriesByContent(db, "local-development-owner", "concise");
        Assert.NotEmpty(memories);
        Assert.Contains(memories, m => m.State == MemoryState.Active);
        Assert.Contains(memories, m => m.Scope == MemoryScope.Global);
    }

    [Fact]
    public async Task WhatIsDependencyInjection_NoMemoryCreated()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "What is dependency injection?"));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = _factory.CreateDbContext();
        var memories = await E2EHelpers.GetAllActiveMemories(db, "local-development-owner");
        Assert.Empty(memories);
    }

    [Fact]
    public async Task ImTiredToday_NoMemoryCreated()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "I'm tired today."));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = _factory.CreateDbContext();
        var memories = await E2EHelpers.GetAllActiveMemories(db, "local-development-owner");
        Assert.Empty(memories);
    }

    [Fact]
    public async Task ConversationHistory_ReachesIngestionPipeline()
    {
        var request = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages = [
                new Message { Role = "user", Content = "I'm working on DeveloperMemory.Api." },
                new Message { Role = "assistant", Content = "Understood." },
                new Message { Role = "user", Content = "This project uses PostgreSQL for persistent memory." }
            ],
            Stream = false
        };

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = _factory.CreateDbContext();
        var memories = await E2EHelpers.FindMemoriesByContent(db, "local-development-owner", "PostgreSQL");
        Assert.NotEmpty(memories);
    }

    [Fact]
    public async Task PreferConciseAnswers_MemoryHasCorrectFields()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "Remember that I prefer concise technical answers."));

        await E2EHelpers.SendChatRequest(_client, request);

        using var db = _factory.CreateDbContext();
        var memories = await E2EHelpers.FindMemoriesByContent(db, "local-development-owner", "concise");
        var memory = memories.First();

        Assert.Equal(MemoryState.Active, memory.State);
        Assert.Equal(MemoryScope.Global, memory.Scope);
        Assert.Equal("local-development-owner", memory.OwnerId);
        Assert.False(string.IsNullOrEmpty(memory.Content));
        Assert.True(memory.CreatedAt > DateTime.MinValue);
        Assert.True(memory.UpdatedAt > DateTime.MinValue);
        Assert.True(memory.Importance >= 0);
        Assert.True(memory.Confidence >= 0);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// RULE 5 + 6: Project inference from conversation + contextual references
// ══════════════════════════════════════════════════════════════════════════════

public class E2E_ProjectInferenceTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;

    public E2E_ProjectInferenceTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task WorkingOnProject_ProjectResolvedAndMemoryAssociated()
    {
        // First: create a project via the Projects API
        var createResponse = await _client.PostAsJsonAsync("/api/Projects",
            new { name = "TestProject", description = "Test project" });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var project = await createResponse.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(project);

        // Now: mention project in conversation - memory should resolve to this project
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", $"I'm working on {project.Name}."),
            ("user", "Remember that this project uses PostgreSQL."));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = _factory.CreateDbContext();
        var memories = await E2EHelpers.FindMemoriesByContent(db, "local-development-owner", "PostgreSQL");
        Assert.NotEmpty(memories);
        var memory = memories.First();
        Assert.Equal(project.Id, memory.ProjectId);
        Assert.Equal(MemoryScope.Project, memory.Scope);
    }

    [Fact]
    public async Task UnknownProject_MemoryStoredAsGlobal()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "I'm working on UnknownProject999."),
            ("user", "Remember that this project uses Redis."));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = _factory.CreateDbContext();
        var memories = await E2EHelpers.FindMemoriesByContent(db, "local-development-owner", "Redis");
        Assert.NotEmpty(memories);
        // Unknown project => Global scope (conservative fallback)
        Assert.Equal(MemoryScope.Global, memories.First().Scope);
    }

    [Fact]
    public async Task NoDuplicateProjectsCreated()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "I'm working on TestProjectX."),
            ("user", "Remember that TestProjectX uses PostgreSQL."));

        await E2EHelpers.SendChatRequest(_client, request);

        using var db = _factory.CreateDbContext();
        var projects = await db.Projects
            .Where(p => p.Name == "TestProjectX")
            .ToListAsync();
        // ConversationalMemoryService does NOT auto-create projects
        Assert.Empty(projects);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// RULE 9: Memory retrieval across separate HTTP requests
// ══════════════════════════════════════════════════════════════════════════════

public class E2E_MemoryRetrievalTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;

    public E2E_MemoryRetrievalTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task TwoRequests_MemoryPersistedInFirstRetrievableInSecond()
    {
        // Request 1: Store memory
        var request1 = E2EHelpers.BuildRequest("stub-model",
            ("user", "Remember that I prefer concise technical answers."));
        var response1 = await E2EHelpers.SendChatRequest(_client, request1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // New request scope (separate HTTP request)
        var request2 = E2EHelpers.BuildRequest("stub-model",
            ("user", "What do you know about how I prefer technical answers?"));
        var response2 = await E2EHelpers.SendChatRequest(_client, request2);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        // The provider should receive enriched context containing the memory
        var forwarded = _factory.Gateway.CapturedRequests[1];
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.Contains("concise", systemMsg);
    }
}
