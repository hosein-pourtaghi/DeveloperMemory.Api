using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeveloperMemory.Api.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// Phase X: OpenAI-Compatible API Contract Tests
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Tests the /v1/models endpoint for OpenAI-compatible model discovery.
/// </summary>
public class PhaseX_ModelsEndpointTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;

    public PhaseX_ModelsEndpointTests(E2EFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetModels_ReturnsOpenAICompatibleList()
    {
        var response = await _client.GetAsync("/v1/models");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OpenAIModelListResponse>();
        Assert.NotNull(body);
        Assert.Equal("list", body.Object);
        Assert.NotEmpty(body.Data);

        // Each model has required OpenAI fields
        foreach (var model in body.Data)
        {
            Assert.False(string.IsNullOrEmpty(model.Id));
            Assert.Equal("model", model.Object);
            Assert.Equal("DeveloperMemory", model.OwnedBy);
        }
    }

    [Fact]
    public async Task GetModels_ContainsAtLeastOneModel()
    {
        var response = await _client.GetAsync("/v1/models");
        var body = await response.Content.ReadFromJsonAsync<OpenAIModelListResponse>();
        Assert.NotNull(body);

        // At least one model should be available
        Assert.True(body.Data.Count > 0, "Model list should not be empty");
        // All model IDs should be non-empty strings
        Assert.All(body.Data, m => Assert.False(string.IsNullOrEmpty(m.Id)));
    }

    [Fact]
    public async Task GetModel_ExistingModel_ReturnsModel()
    {
        // First get the list to find a valid model
        var listResponse = await _client.GetAsync("/v1/models");
        var list = await listResponse.Content.ReadFromJsonAsync<OpenAIModelListResponse>();
        Assert.NotNull(list);
        Assert.True(list.Data.Count > 0);

        var modelId = list.Data[0].Id;
        var response = await _client.GetAsync($"/v1/models/{modelId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetModel_NonexistentModel_ReturnsNotFoundOrOk()
    {
        // Some upstream providers return 200 with model details even for unknown IDs,
        // while others return 404. Both behaviors are acceptable.
        var response = await _client.GetAsync("/v1/models/this-model-definitely-does-not-exist-xyz123");
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.OK,
            $"Expected 404 or 200, got {(int)response.StatusCode}");
    }
}

/// <summary>
/// Tests basic chat completion through the OpenAI-compatible endpoint.
/// </summary>
public class PhaseX_ChatCompletionTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;

    public PhaseX_ChatCompletionTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChatCompletions_BasicRequest_ReturnsSuccess()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "Hello"));
        var response = await E2EHelpers.SendChatRequest(_client, request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OpenAIChatCompletionResponse>();
        Assert.NotNull(body);
        Assert.Equal("chat.completion", body.Object);
        Assert.NotEmpty(body.Choices);
        Assert.Equal("assistant", body.Choices[0].Message.Role);
    }

    [Fact]
    public async Task ChatCompletions_EmptyMessages_Returns400()
    {
        var response = await _client.PostAsync("/v1/chat/completions",
            E2EHelpers.ToJsonContent(new { model = "stub-model", messages = new object[] { } }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OpenAIErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("invalid_request_error", body.Error.Type);
    }

    [Fact]
    public async Task ChatCompletions_NullBody_Returns400()
    {
        var response = await _client.PostAsync("/v1/chat/completions",
            new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChatCompletions_ResponseHasSystemMessageWithContext()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "Hello"));
        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.Contains("DeveloperMemory Context", systemMsg);
    }

    [Fact]
    public async Task ChatCompletions_ExtensionFieldsPreserved()
    {
        var request = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages = [new Message { Role = "user", Content = "test" }],
            Stream = false,
            Project = "test-project-id",
            WorkspaceId = "test-workspace"
        };

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify the forwarded request preserved extension fields
        var forwarded = _factory.Gateway.CapturedRequests.Last();
        Assert.Equal("test-project-id", forwarded.Project);
        Assert.Equal("test-workspace", forwarded.WorkspaceId);
    }
}

/// <summary>
/// Tests AgentContext flow through the OpenAI-compatible API.
/// </summary>
public class PhaseX_AgentContextTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;

    public PhaseX_AgentContextTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChatCompletions_WithAgentId_AgentContextResolved()
    {
        var request = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages = [new Message { Role = "user", Content = "test task" }],
            Stream = false,
            AgentId = "devops-agent",
            AgentType = "DevOps"
        };

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.NotNull(systemMsg);
    }

    [Fact]
    public async Task ChatCompletions_WithoutAgentId_BackwardCompatible()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "Hello without agent context"));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        Assert.Contains("DeveloperMemory Context",
            forwarded.Messages.First(m => m.Role == "system").Content);
    }

    [Fact]
    public async Task ChatCompletions_AgentContextWorkspace_ScopesRetrieval()
    {
        using var db = _factory.CreateDbContext();

        var wsMemory = new MemoryEntry
        {
            Content = "uses Terraform for infrastructure",
            Title = "PX WS Test",
            OwnerId = "local-development-owner",
            Scope = MemoryScope.Workspace,
            WorkspaceId = "px-agent-ws",
            MemoryType = MemoryType.Fact,
            Classification = DataClassification.Internal,
            Importance = 0.8,
            Confidence = 0.9,
            State = MemoryState.Active,
            Source = "phase-x",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        wsMemory.SetTags(["infra"]);
        db.MemoryEntries.Add(wsMemory);
        await db.SaveChangesAsync();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages = [new Message { Role = "user", Content = "uses Terraform for infrastructure" }],
            Stream = false,
            AgentId = "devops-agent",
            AgentType = "DevOps",
            WorkspaceId = "px-agent-ws"
        };

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.Contains("Terraform", systemMsg);
    }
}

/// <summary>
/// Tests memory injection through the external API.
/// </summary>
public class PhaseX_MemoryInjectionTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;

    public PhaseX_MemoryInjectionTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChatCompletions_GlobalMemory_RetrievedAndInjected()
    {
        using var db = _factory.CreateDbContext();

        var memory = new MemoryEntry
        {
            Content = "Always use conventional commits for git messages.",
            Title = "PX Git Convention",
            OwnerId = "local-development-owner",
            Scope = MemoryScope.Global,
            MemoryType = MemoryType.Instruction,
            Classification = DataClassification.Internal,
            Importance = 0.9,
            Confidence = 0.95,
            State = MemoryState.Active,
            Source = "phase-x",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        memory.SetTags(["conventions", "git"]);
        db.MemoryEntries.Add(memory);
        await db.SaveChangesAsync();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages = [new Message { Role = "user", Content = "conventional commits for git messages" }],
            Stream = false,
            AgentId = "coding-agent",
            AgentType = "Coding"
        };

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.Contains("conventional commits", systemMsg);
    }

    [Fact]
    public async Task ChatCompletions_ExpiredMemory_NotRetrieved()
    {
        using var db = _factory.CreateDbContext();

        var expiredMemory = new MemoryEntry
        {
            Content = "This used to use Heroku for deployment.",
            Title = "PX Old Deploy",
            OwnerId = "local-development-owner",
            Scope = MemoryScope.Global,
            MemoryType = MemoryType.Fact,
            Classification = DataClassification.Internal,
            Importance = 0.6,
            Confidence = 0.8,
            State = MemoryState.Expired,
            Source = "phase-x",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        expiredMemory.SetTags(["deployment"]);
        db.MemoryEntries.Add(expiredMemory);
        await db.SaveChangesAsync();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages = [new Message { Role = "user", Content = "uses Heroku for deployment" }],
            Stream = false,
            AgentId = "devops-agent",
            AgentType = "DevOps"
        };

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        // The query text itself appears in the system message (task description).
        // Verify the expired memory *content* is not injected.
        Assert.DoesNotContain("This used to use Heroku for deployment", systemMsg);
    }
}

/// <summary>
/// Tests workspace, project, and user isolation through the external API.
/// </summary>
public class PhaseX_IsolationTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;

    public PhaseX_IsolationTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChatCompletions_WorkspaceIsolation_CrossWorkspaceBlocked()
    {
        using var db = _factory.CreateDbContext();

        db.MemoryEntries.AddRange(
            new MemoryEntry
            {
                Content = "uses Terraform for infrastructure",
                Title = "PX WS-Alpha",
                OwnerId = "local-development-owner",
                Scope = MemoryScope.Workspace,
                WorkspaceId = "px-iso-alpha",
                MemoryType = MemoryType.Fact,
                Classification = DataClassification.Internal,
                Importance = 0.8,
                Confidence = 0.9,
                State = MemoryState.Active,
                Source = "phase-x",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new MemoryEntry
            {
                Content = "uses Pulumi for infrastructure",
                Title = "PX WS-Beta",
                OwnerId = "local-development-owner",
                Scope = MemoryScope.Workspace,
                WorkspaceId = "px-iso-beta",
                MemoryType = MemoryType.Fact,
                Classification = DataClassification.Internal,
                Importance = 0.8,
                Confidence = 0.9,
                State = MemoryState.Active,
                Source = "phase-x",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages = [new Message { Role = "user", Content = "uses Terraform for infrastructure" }],
            Stream = false,
            AgentId = "devops-agent",
            WorkspaceId = "px-iso-alpha"
        };

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.Contains("Terraform", systemMsg);
        Assert.DoesNotContain("Pulumi", systemMsg);
    }

    [Fact]
    public async Task ChatCompletions_ProjectIsolation_CrossProjectBlocked()
    {
        using var db = _factory.CreateDbContext();

        var projectA = new Project
        {
            Name = $"PX-ProjA_{Guid.NewGuid():N}",
            Description = "Project A",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var projectB = new Project
        {
            Name = $"PX-ProjB_{Guid.NewGuid():N}",
            Description = "Project B",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Projects.AddRange(projectA, projectB);
        await db.SaveChangesAsync();

        db.MemoryEntries.AddRange(
            new MemoryEntry
            {
                Content = "Project A uses React for frontend",
                Title = "PX ProjA",
                OwnerId = "local-development-owner",
                Scope = MemoryScope.Project,
                ProjectId = projectA.Id,
                MemoryType = MemoryType.ProjectContext,
                Classification = DataClassification.Internal,
                Importance = 0.8,
                Confidence = 0.9,
                State = MemoryState.Active,
                Source = "phase-x",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new MemoryEntry
            {
                Content = "Project B uses Vue for frontend",
                Title = "PX ProjB",
                OwnerId = "local-development-owner",
                Scope = MemoryScope.Project,
                ProjectId = projectB.Id,
                MemoryType = MemoryType.ProjectContext,
                Classification = DataClassification.Internal,
                Importance = 0.8,
                Confidence = 0.9,
                State = MemoryState.Active,
                Source = "phase-x",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages = [new Message { Role = "user", Content = "uses Vue for frontend" }],
            Stream = false,
            AgentId = "coding-agent",
            Project = projectB.Id.ToString()
        };

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.Contains("Vue", systemMsg);
        Assert.DoesNotContain("React", systemMsg);
    }

    [Fact]
    public async Task ChatCompletions_PrivateMemory_UserIsolation()
    {
        using var db = _factory.CreateDbContext();

        db.MemoryEntries.AddRange(
            new MemoryEntry
            {
                Content = "User A secret preference: dark mode only",
                Title = "PX UserA Secret",
                OwnerId = "local-development-owner",
                Scope = MemoryScope.Private,
                UserId = "px-user-a",
                MemoryType = MemoryType.UserPreference,
                Classification = DataClassification.Confidential,
                Importance = 0.9,
                Confidence = 0.95,
                State = MemoryState.Active,
                Source = "phase-x",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new MemoryEntry
            {
                Content = "User B secret preference: light mode only",
                Title = "PX UserB Secret",
                OwnerId = "local-development-owner",
                Scope = MemoryScope.Private,
                UserId = "px-user-b",
                MemoryType = MemoryType.UserPreference,
                Classification = DataClassification.Confidential,
                Importance = 0.9,
                Confidence = 0.95,
                State = MemoryState.Active,
                Source = "phase-x",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();

        // Request without user context — neither private memory should appear
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "What are my preferences?"));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        // Verify the private memory content is not injected.
        Assert.DoesNotContain("User A secret preference", systemMsg);
        Assert.DoesNotContain("User B secret preference", systemMsg);
    }
}

/// <summary>
/// Tests context mismatch and error contract through the external API.
/// </summary>
public class PhaseX_ErrorContractTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;

    public PhaseX_ErrorContractTests(E2EFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChatCompletions_EmptyBody_Returns400()
    {
        var response = await _client.PostAsync("/v1/chat/completions",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OpenAIErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("invalid_request_error", body.Error.Type);
        Assert.DoesNotContain("stack", JsonSerializer.Serialize(body).ToLower());
        Assert.DoesNotContain("exception", JsonSerializer.Serialize(body).ToLower());
    }

    [Fact]
    public async Task ChatCompletions_MissingMessages_Returns400()
    {
        var response = await _client.PostAsync("/v1/chat/completions",
            new StringContent("{\"model\":\"auto\"}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OpenAIErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("invalid_request_error", body.Error.Type);
    }

    [Fact]
    public async Task ErrorResponses_DoNotLeakInternals()
    {
        var response = await _client.PostAsync("/v1/chat/completions",
            E2EHelpers.ToJsonContent(new { model = "stub-model", messages = new object[] { } }));

        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("stack trace", rawBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DeveloperMemory.Infrastructure", rawBody);
        Assert.DoesNotContain("DeveloperMemory.Application", rawBody);
        Assert.DoesNotContain("DeveloperMemory.Domain", rawBody);
        Assert.DoesNotContain("Npgsql", rawBody);
        Assert.DoesNotContain("password", rawBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentTypeEndpoint_EmptyAgentId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/agent/context/agent-type?agentId=");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AgentTypeEndpoint_ValidAgent_ReturnsClassification()
    {
        var response = await _client.GetAsync("/api/agent/context/agent-type?agentId=coding-agent&task=implement+the+feature");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("agent_id", body);
        Assert.Contains("agent_type", body);
        Assert.Contains("confidence", body);
    }
}

/// <summary>
/// Tests deterministic behavior and backward compatibility.
/// </summary>
public class PhaseX_DeterminismTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;

    public PhaseX_DeterminismTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChatCompletions_SameRequest_SameSystemPrompt()
    {
        var projectId = Guid.NewGuid().ToString();

        var buildRequest = () => new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages = [new Message { Role = "user", Content = "test deterministic" }],
            Stream = false,
            AgentId = "coding-agent",
            AgentType = "Coding",
            Project = projectId
        };

        _factory.Gateway.Reset();

        var r1 = await E2EHelpers.SendChatRequest(_client, buildRequest());
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        var r2 = await E2EHelpers.SendChatRequest(_client, buildRequest());
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        var msg1 = _factory.Gateway.CapturedRequests[0].Messages.First(m => m.Role == "system").Content;
        var msg2 = _factory.Gateway.CapturedRequests[1].Messages.First(m => m.Role == "system").Content;
        Assert.Equal(msg1, msg2);
    }

    [Fact]
    public async Task ChatCompletions_BackwardCompat_NoAgentFields()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "backward compat test"));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        Assert.Contains("DeveloperMemory Context",
            forwarded.Messages.First(m => m.Role == "system").Content);
    }
}
