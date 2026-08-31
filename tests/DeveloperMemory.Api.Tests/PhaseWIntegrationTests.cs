using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeveloperMemory.Api.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// Phase W: Integrated Agent-Aware Memory Intelligence Pipeline
// ══════════════════════════════════════════════════════════════════════════════

public class PhaseW_AgentContextIntegrationTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public PhaseW_AgentContextIntegrationTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AgentContext_FlowsThroughChatCompletion_ReturnsSuccess()
    {
        var storeRequest = E2EHelpers.BuildRequest("stub-model",
            ("user", "Remember that this project uses PostgreSQL for memory storage."));
        var storeResponse = await E2EHelpers.SendChatRequest(_client, storeRequest);
        Assert.Equal(HttpStatusCode.OK, storeResponse.StatusCode);

        var agentRequest = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages =
            [
                new Message { Role = "user", Content = "What database does this project use?" }
            ],
            Stream = false,
            AgentId = "coding-agent",
            AgentType = "Coding"
        };

        var response = await E2EHelpers.SendChatRequest(_client, agentRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.NotNull(systemMsg);
    }

    [Fact]
    public async Task AgentContext_WithoutAgentId_BackwardCompatible()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "Hello, no agent context here."));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        Assert.Contains("DeveloperMemory Context",
            forwarded.Messages.First(m => m.Role == "system").Content);
    }

    [Fact]
    public async Task AgentContext_WorkspaceContext_AppliedToRetrieval()
    {
        using var db = _factory.CreateDbContext();

        var wsMemory = new MemoryEntry
        {
            Content = "Workspace Alpha uses Terraform for infrastructure.",
            Title = "WS Alpha Infra",
            OwnerId = "local-development-owner",
            Scope = MemoryScope.Workspace,
            WorkspaceId = "workspace-alpha",
            MemoryType = MemoryType.Fact,
            Classification = DataClassification.Internal,
            Importance = 0.8,
            Confidence = 0.9,
            State = MemoryState.Active,
            Source = "test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        wsMemory.SetTags(["infrastructure"]);
        db.MemoryEntries.Add(wsMemory);
        await db.SaveChangesAsync();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages =
            [
                new Message { Role = "user", Content = "uses Terraform for infrastructure" }
            ],
            Stream = false,
            AgentId = "devops-agent",
            AgentType = "DevOps",
            WorkspaceId = "workspace-alpha"
        };

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.Contains("Terraform", systemMsg);
    }

    [Fact]
    public async Task AgentContext_ProjectIsolation_ProjectAMemoryNotVisibleForProjectB()
    {
        using var db = _factory.CreateDbContext();

        var projectA = new Project
        {
            Name = $"ProjectA_{Guid.NewGuid():N}",
            Description = "Project A",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var projectB = new Project
        {
            Name = $"ProjectB_{Guid.NewGuid():N}",
            Description = "Project B",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Projects.AddRange(projectA, projectB);
        await db.SaveChangesAsync();

        var memoryA = new MemoryEntry
        {
            Content = "Project A uses React for frontend.",
            Title = "Project A Frontend",
            OwnerId = "local-development-owner",
            Scope = MemoryScope.Project,
            ProjectId = projectA.Id,
            MemoryType = MemoryType.ProjectContext,
            Classification = DataClassification.Internal,
            Importance = 0.8,
            Confidence = 0.9,
            State = MemoryState.Active,
            Source = "test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        memoryA.SetTags(["frontend"]);

        var memoryB = new MemoryEntry
        {
            Content = "Project B uses Vue for frontend.",
            Title = "Project B Frontend",
            OwnerId = "local-development-owner",
            Scope = MemoryScope.Project,
            ProjectId = projectB.Id,
            MemoryType = MemoryType.ProjectContext,
            Classification = DataClassification.Internal,
            Importance = 0.8,
            Confidence = 0.9,
            State = MemoryState.Active,
            Source = "test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        memoryB.SetTags(["frontend"]);
        db.MemoryEntries.AddRange(memoryA, memoryB);
        await db.SaveChangesAsync();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages =
            [
                new Message { Role = "user", Content = "uses Vue for frontend" }
            ],
            Stream = false,
            AgentId = "coding-agent",
            AgentType = "Coding",
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
    public async Task AgentContext_PrivateMemory_UserIsolation()
    {
        using var db = _factory.CreateDbContext();

        var privateMemoryUserA = new MemoryEntry
        {
            Content = "User A secret preference: dark mode only.",
            Title = "User A Secret",
            OwnerId = "local-development-owner",
            Scope = MemoryScope.Private,
            UserId = "user-a-identity",
            MemoryType = MemoryType.UserPreference,
            Classification = DataClassification.Confidential,
            Importance = 0.9,
            Confidence = 0.95,
            State = MemoryState.Active,
            Source = "test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        privateMemoryUserA.SetTags(["preferences"]);

        var privateMemoryUserB = new MemoryEntry
        {
            Content = "User B secret preference: light mode only.",
            Title = "User B Secret",
            OwnerId = "local-development-owner",
            Scope = MemoryScope.Private,
            UserId = "user-b-identity",
            MemoryType = MemoryType.UserPreference,
            Classification = DataClassification.Confidential,
            Importance = 0.9,
            Confidence = 0.95,
            State = MemoryState.Active,
            Source = "test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        privateMemoryUserB.SetTags(["preferences"]);
        db.MemoryEntries.AddRange(privateMemoryUserA, privateMemoryUserB);
        await db.SaveChangesAsync();

        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "What are my preferences?"));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.DoesNotContain("dark mode only", systemMsg);
        Assert.DoesNotContain("light mode only", systemMsg);
    }

    [Fact]
    public async Task AgentContext_GlobalMemory_EligibleAcrossAgentTypes()
    {
        using var db = _factory.CreateDbContext();

        var globalMemory = new MemoryEntry
        {
            Content = "Always use conventional commits for git messages.",
            Title = "Git Convention",
            OwnerId = "local-development-owner",
            Scope = MemoryScope.Global,
            MemoryType = MemoryType.Instruction,
            Classification = DataClassification.Internal,
            Importance = 0.9,
            Confidence = 0.95,
            State = MemoryState.Active,
            Source = "test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        globalMemory.SetTags(["conventions", "git"]);
        db.MemoryEntries.Add(globalMemory);
        await db.SaveChangesAsync();

        var codingRequest = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages =
            [
                new Message { Role = "user", Content = "conventional commits for git messages" }
            ],
            Stream = false,
            AgentId = "coding-agent",
            AgentType = "Coding"
        };

        var response = await E2EHelpers.SendChatRequest(_client, codingRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.Contains("conventional commits", systemMsg);
    }

    [Fact]
    public async Task AgentContext_LifecycleFiltering_ExpiredNotReturned()
    {
        using var db = _factory.CreateDbContext();

        var expiredMemory = new MemoryEntry
        {
            Content = "This project used to use Heroku for deployment.",
            Title = "Old Deployment",
            OwnerId = "local-development-owner",
            Scope = MemoryScope.Global,
            MemoryType = MemoryType.Fact,
            Classification = DataClassification.Internal,
            Importance = 0.6,
            Confidence = 0.8,
            State = MemoryState.Expired,
            Source = "test",
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
            Messages =
            [
                new Message { Role = "user", Content = "Where do we deploy this app?" }
            ],
            Stream = false,
            AgentId = "devops-agent",
            AgentType = "DevOps"
        };

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.DoesNotContain("Heroku", systemMsg);
    }

    [Fact]
    public async Task AgentContext_ExplicitProjectOverridesInferred()
    {
        using var db = _factory.CreateDbContext();

        var projectX = new Project
        {
            Name = $"ProjectX_{Guid.NewGuid():N}",
            Description = "Project X",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Projects.Add(projectX);
        await db.SaveChangesAsync();

        var memoryX = new MemoryEntry
        {
            Content = "Project X uses Rust for backend services.",
            Title = "Project X Backend",
            OwnerId = "local-development-owner",
            Scope = MemoryScope.Project,
            ProjectId = projectX.Id,
            MemoryType = MemoryType.ProjectContext,
            Classification = DataClassification.Internal,
            Importance = 0.8,
            Confidence = 0.9,
            State = MemoryState.Active,
            Source = "test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        memoryX.SetTags(["backend"]);
        db.MemoryEntries.Add(memoryX);
        await db.SaveChangesAsync();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages =
            [
                new Message { Role = "user", Content = "uses Rust for backend services" }
            ],
            Stream = false,
            AgentId = "coding-agent",
            AgentType = "Coding",
            Project = projectX.Id.ToString()
        };

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.Contains("Rust", systemMsg);
    }

    [Fact]
    public async Task AgentContext_SharedMemory_AccessibleByMultipleAgentTypes()
    {
        using var db = _factory.CreateDbContext();

        var sharedMemory = new MemoryEntry
        {
            Content = "The codebase follows Clean Architecture with 4-project separation.",
            Title = "Architecture",
            OwnerId = "local-development-owner",
            Scope = MemoryScope.Global,
            MemoryType = MemoryType.ArchitectureDecision,
            Classification = DataClassification.Internal,
            Importance = 0.9,
            Confidence = 0.95,
            State = MemoryState.Active,
            Source = "test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        sharedMemory.SetTags(["architecture"]);
        db.MemoryEntries.Add(sharedMemory);
        await db.SaveChangesAsync();

        var codingRequest = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages =
            [
                new Message { Role = "user", Content = "follows Clean Architecture" }
            ],
            Stream = false,
            AgentId = "coding-agent",
            AgentType = "Coding"
        };
        var codingResponse = await E2EHelpers.SendChatRequest(_client, codingRequest);
        Assert.Equal(HttpStatusCode.OK, codingResponse.StatusCode);

        var codingForwarded = _factory.Gateway.CapturedRequests.Last();
        var codingMsg = codingForwarded.Messages.First(m => m.Role == "system").Content;
        Assert.Contains("Clean Architecture", codingMsg);

        var docRequest = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages =
            [
                new Message { Role = "user", Content = "4-project separation" }
            ],
            Stream = false,
            AgentId = "docs-agent",
            AgentType = "Documentation"
        };
        var docResponse = await E2EHelpers.SendChatRequest(_client, docRequest);
        Assert.Equal(HttpStatusCode.OK, docResponse.StatusCode);

        var docForwarded = _factory.Gateway.CapturedRequests.Last();
        var docMsg = docForwarded.Messages.First(m => m.Role == "system").Content;
        Assert.Contains("Clean Architecture", docMsg);
    }

    [Fact]
    public async Task AgentContext_Deterministic_SameInputsSameOutput()
    {
        var projectId = Guid.NewGuid().ToString();
        var buildRequest = () => new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages =
            [
                new Message { Role = "user", Content = "What is the project structure?" }
            ],
            Stream = false,
            AgentId = "coding-agent",
            AgentType = "Coding",
            Project = projectId
        };

        _factory.Gateway.Reset();
        var response1 = await E2EHelpers.SendChatRequest(_client, buildRequest());
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        var response2 = await E2EHelpers.SendChatRequest(_client, buildRequest());
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        var forwarded1 = _factory.Gateway.CapturedRequests[0];
        var forwarded2 = _factory.Gateway.CapturedRequests[1];
        var sysMsg1 = forwarded1.Messages.First(m => m.Role == "system").Content;
        var sysMsg2 = forwarded2.Messages.First(m => m.Role == "system").Content;
        Assert.Equal(sysMsg1, sysMsg2);
    }

    [Fact]
    public async Task AgentTypeEndpoint_ReturnsCorrectClassification()
    {
        var response = await _client.GetAsync("/api/agent/context/agent-type?agentId=coding-agent&task=implement+the+feature");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("agent_id", body);
        Assert.Contains("confidence", body);
        Assert.Contains("agent_type", body);
    }

    [Fact]
    public async Task AgentTypeEndpoint_EmptyAgentId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/agent/context/agent-type?agentId=");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Phase W: Security isolation integration tests
// ══════════════════════════════════════════════════════════════════════════════

public class PhaseW_SecurityIsolationTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;

    public PhaseW_SecurityIsolationTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task WorkspaceIsolation_WorkspaceAMemoryNotVisibleInWorkspaceB()
    {
        using var db = _factory.CreateDbContext();

        var wsAMemory = new MemoryEntry
        {
            Content = "Workspace A uses AWS for cloud.",
            Title = "WS A Cloud",
            OwnerId = "local-development-owner",
            Scope = MemoryScope.Workspace,
            WorkspaceId = "workspace-a",
            MemoryType = MemoryType.ProjectContext,
            Classification = DataClassification.Internal,
            Importance = 0.8,
            Confidence = 0.9,
            State = MemoryState.Active,
            Source = "test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        wsAMemory.SetTags(["cloud"]);

        var wsBMemory = new MemoryEntry
        {
            Content = "Workspace B uses GCP for cloud.",
            Title = "WS B Cloud",
            OwnerId = "local-development-owner",
            Scope = MemoryScope.Workspace,
            WorkspaceId = "workspace-b",
            MemoryType = MemoryType.ProjectContext,
            Classification = DataClassification.Internal,
            Importance = 0.8,
            Confidence = 0.9,
            State = MemoryState.Active,
            Source = "test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        wsBMemory.SetTags(["cloud"]);
        db.MemoryEntries.AddRange(wsAMemory, wsBMemory);
        await db.SaveChangesAsync();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "stub-model",
            Messages =
            [
                new Message { Role = "user", Content = "uses GCP for cloud" }
            ],
            Stream = false,
            AgentId = "devops-agent",
            AgentType = "DevOps",
            WorkspaceId = "workspace-b"
        };

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;

        Assert.Contains("GCP", systemMsg);
        Assert.DoesNotContain("AWS", systemMsg);
    }

    [Fact]
    public async Task ContextMismatch_InvalidCombinationRejected()
    {
        var response = await _client.GetAsync("/api/agent/context/agent-type?agentId=");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NoAgentContext_PipelineStillWorks()
    {
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "Remember that I prefer TypeScript for frontend."),
            ("user", "What do you know about my preferences?"));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var systemMsg = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.Contains("DeveloperMemory Context", systemMsg);
    }
}
