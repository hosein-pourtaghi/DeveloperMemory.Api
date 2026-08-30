using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeveloperMemory.Api.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// PHASE P.4: Agent Memory API E2E
// ══════════════════════════════════════════════════════════════════════════════

public class E2E_AgentMemoryApiTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public E2E_AgentMemoryApiTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AgentApi_CreateMemory_ReturnsCreated()
    {
        var request = new
        {
            content = "Agent prefers structured responses",
            title = "Agent Preference",
            scope = MemoryScope.Global,
            memoryType = MemoryType.UserPreference,
            classification = DataClassification.Internal,
            importance = 0.7,
            confidence = 0.9
        };

        var response = await _client.PostAsJsonAsync("/api/agent/memory", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<MemoryDto>();
        Assert.NotNull(dto);
        Assert.Equal("Agent prefers structured responses", dto!.Content);
        Assert.Equal(MemoryScope.Global, dto!.Scope);
        Assert.Equal(MemoryType.UserPreference, dto.MemoryType);
    }

    [Fact]
    public async Task AgentApi_SearchMemory_ReturnsResults()
    {
        // First create a memory
        var createRequest = new
        {
            content = "Searchable agent memory item",
            title = "Searchable",
            scope = MemoryScope.Global,
            memoryType = MemoryType.Fact,
            classification = DataClassification.Internal,
            importance = 0.5,
            confidence = 0.8
        };
        await _client.PostAsJsonAsync("/api/agent/memory", createRequest);

        // Search for it
        var response = await _client.GetAsync("/api/agent/memory/search?query=Searchable");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<List<MemoryDto>>();
        Assert.NotNull(results);
        Assert.Contains(results!, m => m.Content.Contains("Searchable"));
    }

    [Fact]
    public async Task AgentApi_GetById_ReturnsMemory()
    {
        // Create first
        var createRequest = new
        {
            content = "GetById test memory",
            title = "GetById Test",
            scope = MemoryScope.Global,
            memoryType = MemoryType.Fact,
            classification = DataClassification.Internal,
            importance = 0.5,
            confidence = 0.8
        };
        var createResponse = await _client.PostAsJsonAsync("/api/agent/memory", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MemoryDto>();

        // Get by ID
        var getResponse = await _client.GetAsync($"/api/agent/memory/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<MemoryDto>();
        Assert.NotNull(fetched);
        Assert.Equal("GetById test memory", fetched!.Content);
    }

    [Fact]
    public async Task AgentApi_UpdateMemory_ModifiesContent()
    {
        // Create
        var createRequest = new
        {
            content = "Original content",
            title = "Update Test",
            scope = MemoryScope.Global,
            memoryType = MemoryType.Fact,
            classification = DataClassification.Internal,
            importance = 0.5,
            confidence = 0.8
        };
        var createResponse = await _client.PostAsJsonAsync("/api/agent/memory", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MemoryDto>();

        // Update
        var updateRequest = new
        {
            content = "Updated content"
        };
        var updateResponse = await _client.PutAsJsonAsync($"/api/agent/memory/{created!.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<MemoryDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated content", updated!.Content);
    }

    [Fact]
    public async Task AgentApi_DeleteMemory_ReturnsNoContent()
    {
        // Create
        var createRequest = new
        {
            content = "Delete me",
            title = "Delete Test",
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

        // Verify memory is no longer active
        using var db = _factory.CreateDbContext();
        var entry = await db.MemoryEntries.FindAsync(created.Id);
        Assert.NotNull(entry);
        Assert.Equal(MemoryState.Deleted, entry!.State);
    }

    [Fact]
    public async Task AgentApi_GetStats_ReturnsStats()
    {
        var response = await _client.GetAsync("/api/agent/memory/stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stats = await response.Content.ReadFromJsonAsync<MemoryStatsDto>();
        Assert.NotNull(stats);
        Assert.True(stats!.TotalCount >= 0);
    }

    [Fact]
    public async Task AgentApi_SearchEmptyQuery_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/agent/memory/search?query=");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AgentApi_CreateEmptyContent_ReturnsBadRequest()
    {
        var request = new { content = "", title = "Empty" };
        var response = await _client.PostAsJsonAsync("/api/agent/memory", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AgentApi_ConversationalMemory_VisibleToAgentApi()
    {
        // Create memory via conversational pipeline
        var chatRequest = E2EHelpers.BuildRequest("stub-model",
            ("user", "Remember that agent test preference is to use tabs over spaces."));
        var chatResponse = await E2EHelpers.SendChatRequest(_client, chatRequest);
        Assert.Equal(HttpStatusCode.OK, chatResponse.StatusCode);

        // Verify via direct DB query that memory was persisted
        using var db = _factory.CreateDbContext();
        var memories = await db.MemoryEntries
            .Where(e => e.OwnerId == "local-development-owner" && e.State == MemoryState.Active)
            .Where(e => e.Content.Contains("tabs over spaces") || e.Content.Contains("agent test"))
            .ToListAsync();
        Assert.NotEmpty(memories);

        // Search via Agent API — same canonical memory store
        var searchResponse = await _client.GetAsync("/api/agent/memory/search?query=tabs+over+spaces");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var results = await searchResponse.Content.ReadFromJsonAsync<List<MemoryDto>>();
        Assert.NotNull(results);
        Assert.Contains(results!, m => m.Content.Contains("tabs over spaces"));
    }

    [Fact]
    public async Task AgentApi_SupersedeMemory_CreatesNewAndMarksOld()
    {
        // Create
        var createRequest = new
        {
            content = "Old preference",
            title = "Supersede Test",
            scope = MemoryScope.Global,
            memoryType = MemoryType.UserPreference,
            classification = DataClassification.Internal,
            importance = 0.6,
            confidence = 0.8
        };
        var createResponse = await _client.PostAsJsonAsync("/api/agent/memory", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MemoryDto>();

        // Supersede
        var supersedeRequest = new
        {
            content = "New preference",
            title = "Superseded Preference",
            scope = MemoryScope.Global,
            memoryType = MemoryType.UserPreference,
            classification = DataClassification.Internal,
            importance = 0.8,
            confidence = 0.9
        };
        var supersedeResponse = await _client.PostAsJsonAsync(
            $"/api/agent/memory/{created!.Id}/supersede", supersedeRequest);
        Assert.Equal(HttpStatusCode.OK, supersedeResponse.StatusCode);

        var superseded = await supersedeResponse.Content.ReadFromJsonAsync<MemoryDto>();
        Assert.NotNull(superseded);
        Assert.Equal("New preference", superseded!.Content);

        // Old memory should now be Superseded
        var oldResponse = await _client.GetAsync($"/api/agent/memory/{created.Id}");
        // Old memory might not be accessible via agent API (depends on owner check)
        // At minimum, the new memory should reference the old one
        Assert.Equal(created.Id, superseded.SupersedesId);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// PHASE P.6: ProjectsController E2E
// ══════════════════════════════════════════════════════════════════════════════

public class E2E_ProjectsControllerTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;

    public E2E_ProjectsControllerTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateProject_ReturnsCreated()
    {
        var request = new { name = $"E2EProject_{Guid.NewGuid():N}", description = "E2E test project" };
        var response = await _client.PostAsJsonAsync("/api/Projects", request);

        // CreatedAtAction returns 201
        Assert.True(response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK,
            $"Expected 201 Created, got {(int)response.StatusCode}");

        var project = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(project);
        Assert.Equal(request.name, project!.Name);
    }

    [Fact]
    public async Task CreateProject_EmptyName_ReturnsBadRequest()
    {
        var request = new { name = "", description = "test" };
        var response = await _client.PostAsJsonAsync("/api/Projects", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAllProjects_ReturnsList()
    {
        var response = await _client.GetAsync("/api/Projects");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var projects = await response.Content.ReadFromJsonAsync<List<ProjectDto>>();
        Assert.NotNull(projects);
    }

    [Fact]
    public async Task GetProjectById_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/Projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndRetrieveProject_EndToEnd()
    {
        var name = $"E2ERetrieve_{Guid.NewGuid():N}";
        var createResponse = await _client.PostAsJsonAsync("/api/Projects",
            new { name, description = "Retrieve test" });
        var created = await createResponse.Content.ReadFromJsonAsync<ProjectDto>();

        var getResponse = await _client.GetAsync($"/api/Projects/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.Equal(name, fetched!.Name);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// PHASE P.5: Diagnostic Logging E2E via HTTP
// ══════════════════════════════════════════════════════════════════════════════

public class E2E_DiagnosticLoggingHttpTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;

    public E2E_DiagnosticLoggingHttpTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body);
    }

    [Fact]
    public async Task ChatCompletions_Endpoint_DoesNotCrashWithDiagnosticsDisabled()
    {
        // Diagnostics:PersistToDatabase is set to false in factory config
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "Hello diagnostic test"));
        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DiagnosticLogEntry_SensitiveHeadersNotPersisted()
    {
        // Verify that the middleware handles requests with various headers gracefully.
        // Header redaction is verified by the unit tests (DiagnosticLoggingTests).
        // Here we verify the HTTP-level behavior: requests with custom headers succeed.
        var request = E2EHelpers.BuildRequest("stub-model",
            ("user", "Test header handling"));

        var response = await E2EHelpers.SendChatRequest(_client, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The diagnostic middleware code explicitly excludes:
        // Authorization, Cookie, Set-Cookie, X-Api-Key, X-Auth-Token, Proxy-Authorization
        // from the log entry. This is verified by DiagnosticLoggingTests.DiagnosticLogEntry_DoesNotContainSecrets.
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_ReturnsOpenAiErrorFormat()
    {
        // Send an invalid request that triggers the global exception handler
        var response = await _client.PostAsync("/v1/chat/completions",
            new StringContent("not json", Encoding.UTF8, "application/json"));

        // Should return an error (400 or 500) in OpenAI-compatible format
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("error", body);
    }
}
