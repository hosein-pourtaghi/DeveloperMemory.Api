using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DeveloperMemory.Api.Abstractions;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DeveloperMemory.Api.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// V2-2: Assistant API contract tests (POST /api/agent/assistant)
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// IModelGateway stub that can be told to throw a provider error,
/// used to verify the assistant endpoint's model-failure mapping.
/// </summary>
public class ThrowingCaptureModelGateway : IModelGateway
{
    public bool IsConfigured { get; set; } = true;
    public DownstreamProviderException? ExceptionToThrow { get; set; }

    public Task<OpenAIChatCompletionResponse> SendCompletionAsync(
        OpenAIChatCompletionRequest request,
        CancellationToken ct = default)
    {
        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(new OpenAIChatCompletionResponse
        {
            Id = "chatcmpl-throw",
            Model = "stub-model",
            Choices = [new() { Message = new Message { Role = "assistant", Content = "stub response" } }]
        });
    }

    public Task<Stream> SendStreamingCompletionAsync(OpenAIChatCompletionRequest request, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<List<string>> GetModelsAsync(CancellationToken ct = default)
        => Task.FromResult(new List<string> { "stub-model" });

    public Task<OpenAIModel?> GetModelAsync(string modelId, CancellationToken ct = default)
        => Task.FromResult<OpenAIModel?>(new OpenAIModel { Id = modelId });

    public string ResolveModel(string? requestedModel)
        => requestedModel ?? "stub-model";
}

/// <summary>
/// Factory with a throwing gateway for model-failure tests.
/// </summary>
public class AssistantFailureE2EFactory : E2EFactory
{
    public ThrowingCaptureModelGateway FailingGateway { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IModelGateway>();
            services.AddSingleton<IModelGateway>(FailingGateway);
        });
    }
}

/// <summary>
/// Factory with the development auth bypass disabled, for unauthorized tests.
/// </summary>
public class AssistantNoAuthE2EFactory : E2EFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Appended after the base configuration, so this overrides the bypass flag.
        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:DevelopmentBypass"] = "false"
            });
        });
    }
}

public class AssistantApiTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;

    public AssistantApiTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent JsonBody(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private static object BuildValidBody(string task = "Summarize the git convention") => new
    {
        task,
        workspace_id = "ws-main",
        assistant_id = "assistant",
        instructions = "Always answer in one paragraph.",
        constraints = new[] { "be concise" }
    };

    // ── Valid request → successful model response ──

    [Fact]
    public async Task Execute_ValidRequest_ReturnsAssistantResponse()
    {
        var response = await _client.PostAsync("/api/agent/assistant", JsonBody(BuildValidBody()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("stub response", body.GetProperty("response").GetString());
        Assert.Equal("stub-model", body.GetProperty("model").GetString());
        // Enum status serializes as number: 0 = Success.
        Assert.Equal(0, body.GetProperty("status").GetInt32());
        Assert.True(body.GetProperty("modelCalled").GetBoolean());
        Assert.True(body.GetProperty("execution").GetProperty("totalDurationMs").GetDouble() >= 0);
    }

    // ── Context consumption: UnifiedAgentContext assembled and used ──

    [Fact]
    public async Task Execute_ValidRequest_ModelAbstractionReceivesUnifiedContext()
    {
        using var db = _factory.CreateDbContext();
        db.MemoryEntries.Add(new MemoryEntry
        {
            Title = "V2-2 Git Convention",
            Content = "The team uses conventional commits for git messages.",
            OwnerId = "local-development-owner",
            Scope = MemoryScope.Global,
            MemoryType = MemoryType.Instruction,
            Classification = DataClassification.Internal,
            Importance = 0.9,
            Confidence = 0.95,
            State = MemoryState.Active,
            Source = "v2-2-test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var response = await _client.PostAsync("/api/agent/assistant",
            JsonBody(BuildValidBody("conventional commits for git messages")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The assistant reached the configured model abstraction with a request
        // built from the assembled UnifiedAgentContext.
        Assert.True(_factory.Gateway.CallCount >= 1, "assistant must reach the model abstraction");
        var forwarded = _factory.Gateway.CapturedRequests.Last();

        var system = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.Contains("Runtime Context (current execution only)", system);
        Assert.Contains("Persistent Intelligence (read-only reference data", system);
        Assert.Contains("conventional commits", system); // persistent memory injected

        var runtimeStart = system.IndexOf("Runtime Context (current execution only)", StringComparison.Ordinal);
        var persistentStart = system.IndexOf("Persistent Intelligence (read-only reference data", StringComparison.Ordinal);
        Assert.True(runtimeStart >= 0 && persistentStart > runtimeStart,
            "runtime and persistent context must remain distinguishable blocks");

        // The user request is the final user message.
        Assert.Equal("conventional commits for git messages",
            forwarded.Messages.Last(m => m.Role == "user").Content);
    }

    // ── Request errors ──

    [Fact]
    public async Task Execute_EmptyTask_Returns400()
    {
        var response = await _client.PostAsync("/api/agent/assistant", JsonBody(new { task = "   " }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_error", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_MalformedBody_Returns400()
    {
        var response = await _client.PostAsync("/api/agent/assistant",
            new StringContent("{\"task\":", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Execute_EmptyBody_Returns400()
    {
        var response = await _client.PostAsync("/api/agent/assistant",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Model failure → appropriate error response ──

    [Fact]
    public async Task Execute_ModelRateLimited_Returns429WithSafeError()
    {
        var factory = new AssistantFailureE2EFactory();
        using var client = factory.CreateClient();
        factory.FailingGateway.ExceptionToThrow =
            new DownstreamProviderException(HttpStatusCode.TooManyRequests, "{\"error\":\"rate limited\"}");

        var response = await client.PostAsync("/api/agent/assistant", JsonBody(BuildValidBody()));

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("model_rate_limited", body.GetProperty("error").GetProperty("code").GetString());
        // No provider internals leaked.
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("rate limited\"", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", raw, StringComparison.OrdinalIgnoreCase);
    }

    // ── Unauthorized request ──

    [Fact]
    public async Task Execute_WithoutAuthentication_Returns401()
    {
        var factory = new AssistantNoAuthE2EFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/agent/assistant", JsonBody(BuildValidBody()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

/// <summary>
/// Contract tests for the assistant abstraction surface (provider independence).
/// </summary>
public class AssistantAbstractionContractTests
{
    [Fact]
    public void AssistantOrchestrator_DependsOnApplicationAbstractions()
    {
        var type = typeof(DeveloperMemory.Application.Contracts.IAssistantOrchestrator);
        var method = type.GetMethod(nameof(DeveloperMemory.Application.Contracts.IAssistantOrchestrator.ExecuteAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<DeveloperMemory.Application.Contracts.AssistantExecutionResult>), method!.ReturnType);

        foreach (var parameter in method.GetParameters())
        {
            Assert.False(parameter.ParameterType.Name.Contains("FreeLlm", StringComparison.OrdinalIgnoreCase));
            Assert.False(parameter.ParameterType.Name.Contains("HttpClient", StringComparison.OrdinalIgnoreCase));
            Assert.False(parameter.ParameterType.Name.Contains("OpenAI", StringComparison.OrdinalIgnoreCase));
            Assert.False(parameter.ParameterType.Name.Contains("DbContext", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void AssistantModelGatewayExecutor_ImplementsApplicationPort()
    {
        var type = typeof(DeveloperMemory.Api.Services.AssistantModelGatewayExecutor);
        Assert.True(typeof(DeveloperMemory.Application.Contracts.IAssistantModelExecutor).IsAssignableFrom(type));
    }

    [Fact]
    public void AssistantExecutionRequest_ReusesV2ContextBoundary()
    {
        var request = new DeveloperMemory.Application.Contracts.AssistantExecutionRequest
        {
            Task = "task",
            ProjectId = Guid.NewGuid(),
            MaxResults = 5,
            ContextTokenBudget = 2000
        };

        Assert.Equal("task", request.Task);
        Assert.Equal(5, request.MaxResults);
        Assert.Equal(2000, request.ContextTokenBudget);
    }
}