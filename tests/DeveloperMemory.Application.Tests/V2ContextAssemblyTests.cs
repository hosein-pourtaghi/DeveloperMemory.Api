using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// V2-1: ContextAssemblyService unit tests (mocked retrieval/project providers)
// ══════════════════════════════════════════════════════════════════════════════

public class ContextAssemblyServiceTests
{
    private readonly Mock<IMemoryRetrievalService> _mockRetrieval;
    private readonly Mock<IProjectContextProvider> _mockProjectProvider;
    private readonly Mock<IAgentContextProvider> _mockAgentProvider;
    private readonly Mock<ILogger<ContextAssemblyService>> _mockLogger;
    private readonly ContextAssemblyService _service;

    public ContextAssemblyServiceTests()
    {
        _mockRetrieval = new Mock<IMemoryRetrievalService>();
        _mockProjectProvider = new Mock<IProjectContextProvider>();
        _mockAgentProvider = new Mock<IAgentContextProvider>();
        _mockLogger = new Mock<ILogger<ContextAssemblyService>>();
        _service = new ContextAssemblyService(
            _mockRetrieval.Object, _mockProjectProvider.Object,
            _mockAgentProvider.Object, _mockLogger.Object);
    }

    private static RetrievedMemoriesResult EmptyResult() =>
        new() { Memories = [], Metadata = new RetrievalMetadata() };

    private static RetrievedMemory Memory(string content, MemoryType type = MemoryType.Fact,
        MemoryScope scope = MemoryScope.Global, double importance = 0.5, double relevance = 0.8,
        Guid? projectId = null, string? workspaceId = null, string? source = null)
    {
        return new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = $"Title {content[..Math.Min(content.Length, 20)]}",
            Content = content,
            MemoryType = type,
            Scope = scope,
            State = MemoryState.Active,
            ProjectId = projectId,
            WorkspaceId = workspaceId,
            Source = source ?? "unit-test",
            Importance = importance,
            RelevanceScore = relevance,
            UpdatedAt = DateTime.UtcNow,
            EstimatedTokens = 10
        };
    }

    // ── Combination: runtime + persistent ──

    [Fact]
    public async Task AssembleAsync_CombinesRuntimeRequestWithPersistentIntelligence()
    {
        var projectId = Guid.NewGuid();
        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedMemoriesResult
            {
                Memories =
                [
                    Memory("Prefer PostgreSQL for persistence", MemoryType.UserPreference),
                    Memory("API uses Clean Architecture", MemoryType.ArchitectureDecision, MemoryScope.Project, projectId: projectId)
                ],
                Metadata = new RetrievalMetadata { CandidateCount = 2, EligibleCount = 2, SelectedCount = 2, EstimatedTokensUsed = 20 }
            });
        _mockProjectProvider.Setup(p => p.IsAvailable).Returns(true);
        _mockProjectProvider.Setup(p => p.GetContextAsync(It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectContext { ProjectId = projectId, ProjectName = "Demo" });

        var result = await _service.AssembleAsync(new UnifiedContextRequest
        {
            Task = "Refactor persistence layer",
            ProjectId = projectId
        }, "owner-1");

        // Runtime partition holds the request; persistent partition holds memories + project knowledge.
        result.Runtime.Request.Should().Be("Refactor persistence layer");
        result.Runtime.ProjectId.Should().Be(projectId);
        result.Persistent.Memories.Should().HaveCount(2);
        result.Persistent.ProjectKnowledge.Should().NotBeNull();
        result.Persistent.ProjectKnowledge!.ProjectName.Should().Be("Demo");
        result.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task AssembleAsync_RuntimeContext_NeverMergedIntoPersistent()
    {
        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult());

        var result = await _service.AssembleAsync(new UnifiedContextRequest
        {
            Task = "Check the deployment pipeline",
            Query = "deployment",
            Constraints = ["never deploy on Fridays"],
            ConversationHistory = ["user: hello", "assistant: hi"],
            Tags = ["devops"],
            WorkspaceId = "ws-main"
        }, "owner-1");

        // Explicit caller instructions/conversation are runtime-only.
        result.Runtime.ExplicitInstructions.Should().Contain("never deploy on Fridays");
        result.Runtime.ConversationHistory.Should().HaveCount(2);
        result.Runtime.Tags.Should().Contain("devops");
        result.Runtime.Query.Should().Be("deployment");
        result.Runtime.WorkspaceId.Should().Be("ws-main");
        // Nothing in the persistent partition reflects the transient request.
        result.Persistent.Memories.Should().BeEmpty();
        result.Persistent.ProjectKnowledge.Should().BeNull();
    }

    // ── Empty-context behavior ──

    [Fact]
    public async Task AssembleAsync_EmptyTask_ReturnsEmptyContextWithoutThrowing()
    {
        var result = await _service.AssembleAsync(new UnifiedContextRequest { Task = "   " }, "owner-1");

        result.Should().NotBeNull();
        result.IsEmpty.Should().BeTrue();
        result.Assembly.Warnings.Should().Contain(w => w.Contains("Task is empty", StringComparison.OrdinalIgnoreCase));
        _mockRetrieval.Verify(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssembleAsync_NoPersistentIntelligence_ContextIsEmpty()
    {
        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult());

        var result = await _service.AssembleAsync(new UnifiedContextRequest { Task = "anything" }, "owner-1");

        result.IsEmpty.Should().BeTrue();
        result.Persistent.Memories.Should().BeEmpty();
        result.Persistent.ProjectKnowledge.Should().BeNull();
        result.Runtime.Request.Should().Be("anything"); // Runtime always preserved
    }

    // ── Scope / project / workspace isolation at the boundary ──

    [Fact]
    public async Task AssembleAsync_ForwardsProjectAndWorkspaceToRetrievalPipeline()
    {
        var projectId = Guid.NewGuid();
        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult());

        await _service.AssembleAsync(new UnifiedContextRequest
        {
            Task = "fix auth",
            ProjectId = projectId,
            WorkspaceId = "ws-1"
        }, "owner-1");

        _mockRetrieval.Verify(r => r.RetrieveAsync(
            It.Is<RetrievalRequest>(req =>
                req.OwnerId == "owner-1" &&
                req.UserId == "owner-1" &&
                req.ProjectId == projectId &&
                req.WorkspaceId == "ws-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssembleAsync_NoProjectNoWorkspace_RetrievalIsolationStaysNeutral()
    {
        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult());

        await _service.AssembleAsync(new UnifiedContextRequest { Task = "general question" }, "owner-1");

        _mockRetrieval.Verify(r => r.RetrieveAsync(
            It.Is<RetrievalRequest>(req => req.ProjectId == null && req.WorkspaceId == null),
            It.IsAny<CancellationToken>()), Times.Once);

        // Project knowledge must not be requested without an active project.
        _mockProjectProvider.Verify(p => p.GetContextAsync(
            It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Limits ──

    [Fact]
    public async Task AssembleAsync_ForwardsLimitsToRetrievalPipeline()
    {
        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult());

        var result = await _service.AssembleAsync(new UnifiedContextRequest
        {
            Task = "task",
            MaxResults = 7,
            ContextTokenBudget = 1200
        }, "owner-1");

        _mockRetrieval.Verify(r => r.RetrieveAsync(
            It.Is<RetrievalRequest>(req => req.MaximumResults == 7 && req.ContextTokenBudget == 1200),
            It.IsAny<CancellationToken>()), Times.Once);
        result.Assembly.MaximumResults.Should().Be(7);
        result.Assembly.TokenBudget.Should().Be(1200);
    }

    // ── Duplicate suppression ──

    [Fact]
    public async Task AssembleAsync_SuppressesIdenticalContentDuplicates()
    {
        var kept = Memory("The team uses PostgreSQL everywhere", importance: 0.9);
        var dup = Memory("The team uses PostgreSQL everywhere", importance: 0.9);

        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedMemoriesResult
            {
                Memories = [kept, dup],
                Metadata = new RetrievalMetadata { CandidateCount = 2, SelectedCount = 2, EligibleCount = 2 }
            });

        var result = await _service.AssembleAsync(new UnifiedContextRequest { Task = "db" }, "owner-1");

        result.Persistent.Memories.Should().ContainSingle();
        result.Assembly.DuplicatesSuppressed.Should().Be(1);
        result.Assembly.SuppressedMemoryIds.Should().Contain(dup.MemoryId);
    }

    [Fact]
    public async Task AssembleAsync_DuplicateSuppression_KeepsHigherImportanceVariant()
    {
        var low = Memory("All services use JSON logging", importance: 0.4);
        var high = Memory("All services use JSON logging", importance: 0.95);

        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedMemoriesResult
            {
                Memories = [low, high],
                Metadata = new RetrievalMetadata()
            });

        var result = await _service.AssembleAsync(new UnifiedContextRequest { Task = "logging" }, "owner-1");

        result.Persistent.Memories.Should().ContainSingle();
        result.Persistent.Memories[0].MemoryId.Should().Be(high.MemoryId);
        result.Assembly.SuppressedMemoryIds.Should().Contain(low.MemoryId);
    }

    // ── Provenance ──

    [Fact]
    public async Task AssembleAsync_PersistentMemoriesPreserveProvenance()
    {
        var projectId = Guid.NewGuid();
        var memory = Memory(
            "ADR: repository pattern for data access", MemoryType.ArchitectureDecision,
            MemoryScope.Project, projectId: projectId, source: "chat-consolidation");
        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedMemoriesResult
            {
                Memories = [memory],
                Metadata = new RetrievalMetadata()
            });

        var result = await _service.AssembleAsync(new UnifiedContextRequest
        {
            Task = "repository",
            ProjectId = projectId
        }, "owner-1");

        var item = result.Persistent.Memories.Single();
        item.MemoryId.Should().Be(memory.MemoryId);
        item.Source.Should().Be("chat-consolidation");
        item.Scope.Should().Be(MemoryScope.Project);
        item.ProjectId.Should().Be(projectId);
        item.MemoryType.Should().Be(MemoryType.ArchitectureDecision);
        item.RelevanceScore.Should().Be(memory.RelevanceScore);
    }

    // ── Project knowledge ──

    [Fact]
    public async Task AssembleAsync_ProjectKnowledgeIncludedForActiveProject()
    {
        var projectId = Guid.NewGuid();
        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult());
        _mockProjectProvider.Setup(p => p.IsAvailable).Returns(true);
        _mockProjectProvider.Setup(p => p.GetContextAsync(It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectContext
            {
                ProjectId = projectId,
                ProjectName = "Alpha",
                TechnologyStack = ["PostgreSQL", ".NET 10"]
            });

        var result = await _service.AssembleAsync(new UnifiedContextRequest
        {
            Task = "query",
            ProjectId = projectId
        }, "owner-1");

        result.Persistent.ProjectKnowledge.Should().NotBeNull();
        result.Persistent.ProjectKnowledge!.TechnologyStack.Should().Contain("PostgreSQL");
        result.Assembly.ProjectKnowledgeIncluded.Should().BeTrue();
        _mockProjectProvider.Verify(p => p.GetContextAsync(
            projectId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssembleAsync_ProjectKnowledgeMissing_ReportsWithoutIncluding()
    {
        var projectId = Guid.NewGuid();
        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult());
        _mockProjectProvider.Setup(p => p.IsAvailable).Returns(true);
        _mockProjectProvider.Setup(p => p.GetContextAsync(It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectContext?)null);

        var result = await _service.AssembleAsync(new UnifiedContextRequest
        {
            Task = "query",
            ProjectId = projectId
        }, "owner-1");

        result.Persistent.ProjectKnowledge.Should().BeNull();
        result.Assembly.ProjectKnowledgeIncluded.Should().BeFalse();
        result.Assembly.Warnings.Should().Contain(w => w.Contains("No project knowledge", StringComparison.OrdinalIgnoreCase));
    }

    // ── Degradation ──

    [Fact]
    public async Task AssembleAsync_RetrievalFailure_DegradesGracefully()
    {
        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("pipeline down"));

        var result = await _service.AssembleAsync(new UnifiedContextRequest { Task = "task" }, "owner-1");

        result.Persistent.Memories.Should().BeEmpty();
        result.Assembly.Warnings.Should().Contain(w => w.Contains("Memory retrieval unavailable", StringComparison.OrdinalIgnoreCase));
        result.Runtime.Request.Should().Be("task");
    }

    // ── Agent-agnostic foundation ──

    [Fact]
    public async Task AssembleAsync_NoAgent_RemainsAgentAgnostic()
    {
        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult());

        var result = await _service.AssembleAsync(new UnifiedContextRequest { Task = "task" }, "owner-1");

        result.Runtime.AgentId.Should().BeNull();
        result.Runtime.AgentType.Should().BeNull();
    }

    [Fact]
    public async Task AssembleAsync_AgentIdWithoutType_ClassifiedByExistingProvider()
    {
        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult());
        _mockAgentProvider.Setup(p => p.Resolve(It.IsAny<AgentContextRequest>()))
            .Returns(new AgentContext { AgentId = "cursor", AgentType = AgentType.Coding });

        var result = await _service.AssembleAsync(new UnifiedContextRequest
        {
            Task = "write feature",
            AgentId = "cursor"
        }, "owner-1");

        result.Runtime.AgentId.Should().Be("cursor");
        result.Runtime.AgentType.Should().Be(AgentType.Coding);
    }

    [Fact]
    public async Task AssembleAsync_ExplicitAgentType_PreservedWithoutClassification()
    {
        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult());

        var result = await _service.AssembleAsync(new UnifiedContextRequest
        {
            Task = "task",
            AgentId = "custom",
            AgentType = AgentType.Testing
        }, "owner-1");

        result.Runtime.AgentType.Should().Be(AgentType.Testing);
        _mockAgentProvider.Verify(p => p.Resolve(It.IsAny<AgentContextRequest>()), Times.Never);
    }

    // ── Query derivation ──

    [Fact]
    public async Task AssembleAsync_QueryDerivedFromTaskWhenNotProvided()
    {
        _mockRetrieval.Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult());

        await _service.AssembleAsync(new UnifiedContextRequest { Task = "PostgreSQL schema design" }, "owner-1");

        _mockRetrieval.Verify(r => r.RetrieveAsync(
            It.Is<RetrievalRequest>(req => req.Query == "PostgreSQL schema design"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// V2-1: ContextAssemblyService tests over the REAL retrieval pipeline
// (EF Core InMemory + KeywordRetrievalProvider + RelevanceRanker + Budgeter)
// ══════════════════════════════════════════════════════════════════════════════

public class ContextAssemblyPipelineTests : IDisposable
{
    private readonly DeveloperMemoryDbContext _context;
    private readonly ContextAssemblyService _service;
    private readonly Mock<IProjectContextProvider> _mockProjectProvider;

    public ContextAssemblyPipelineTests()
    {
        var options = new DbContextOptionsBuilder<DeveloperMemoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new DeveloperMemoryDbContext(options);
        _context.Database.EnsureCreated();

        var provider = new KeywordRetrievalProvider(_context);
        var ranker = new RelevanceRanker();
        var budgeter = new CharacterContextBudgeter();
        var retrievalService = new MemoryRetrievalService(
            provider, ranker, budgeter, new Mock<ILogger<MemoryRetrievalService>>().Object);

        _mockProjectProvider = new Mock<IProjectContextProvider>();
        _mockProjectProvider.Setup(p => p.IsAvailable).Returns(true);
        _mockProjectProvider.Setup(p => p.GetContextAsync(It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectContext?)null);

        _service = new ContextAssemblyService(
            retrievalService, _mockProjectProvider.Object,
            new AgentContextProvider(), new Mock<ILogger<ContextAssemblyService>>().Object);
    }

    public void Dispose() => _context.Dispose();

    private async Task AddAsync(params MemoryEntry[] entries)
    {
        _context.MemoryEntries.AddRange(entries);
        await _context.SaveChangesAsync();
    }

    private static MemoryEntry Entry(string title, MemoryScope scope = MemoryScope.Global,
        MemoryState state = MemoryState.Active, Guid? projectId = null, string? workspaceId = null,
        string? userId = null, string ownerId = "owner-1", string? source = null)
    {
        var entry = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Title = title,
            Content = $"{title} details about config and architecture",
            Scope = scope,
            State = state,
            ProjectId = projectId,
            WorkspaceId = workspaceId,
            UserId = userId,
            OwnerId = ownerId,
            Source = source ?? "pipeline-test",
            Importance = 0.6,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return entry;
    }

    // ── Project isolation through the full pipeline ──

    [Fact]
    public async Task AssembleAsync_RealPipeline_ProjectContextDoesNotLeakAcrossProjects()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        await AddAsync(
            Entry("Alpha Config", MemoryScope.Project, projectId: projectA),
            Entry("Beta Config", MemoryScope.Project, projectId: projectB),
            Entry("Shared Global Config", MemoryScope.Global));

        var resultA = await _service.AssembleAsync(new UnifiedContextRequest
        {
            Task = "config",
            ProjectId = projectA
        }, "owner-1");

        resultA.Persistent.Memories.Should().Contain(m => m.Title == "Alpha Config");
        resultA.Persistent.Memories.Should().Contain(m => m.Title == "Shared Global Config");
        resultA.Persistent.Memories.Should().NotContain(m => m.Title == "Beta Config",
            "project B memories must never leak into project A context");
    }

    // ── Workspace isolation through the full pipeline ──

    [Fact]
    public async Task AssembleAsync_RealPipeline_WorkspaceContextDoesNotLeakAcrossWorkspaces()
    {
        await AddAsync(
            Entry("Workspace Alpha Secret", MemoryScope.Workspace, workspaceId: "ws-A"),
            Entry("Workspace Beta Secret", MemoryScope.Workspace, workspaceId: "ws-B"),
            Entry("Global Secret Fact", MemoryScope.Global));

        var result = await _service.AssembleAsync(new UnifiedContextRequest
        {
            Task = "secret",
            WorkspaceId = "ws-A"
        }, "owner-1");

        result.Persistent.Memories.Should().Contain(m => m.Title == "Workspace Alpha Secret");
        result.Persistent.Memories.Should().Contain(m => m.Title == "Global Secret Fact");
        result.Persistent.Memories.Should().NotContain(m => m.Title == "Workspace Beta Secret",
            "workspace B memories must never leak into workspace A context");
    }

    // ── Lifecycle filtering through the full pipeline ──

    [Fact]
    public async Task AssembleAsync_RealPipeline_OnlyActiveAndUpdatedMemoriesSurvive()
    {
        await AddAsync(
            Entry("Active Memory", state: MemoryState.Active),
            Entry("Updated Memory", state: MemoryState.Updated),
            Entry("Deleted Memory", state: MemoryState.Deleted),
            Entry("Superseded Memory", state: MemoryState.Superseded),
            new MemoryEntry
            {
                Id = Guid.NewGuid(),
                Title = "Expired Memory",
                Content = "Expired Memory details about config and architecture",
                Scope = MemoryScope.Global,
                State = MemoryState.Active,
                OwnerId = "owner-1",
                Importance = 0.6,
                ExpiresAt = DateTime.UtcNow.AddDays(-1),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var result = await _service.AssembleAsync(new UnifiedContextRequest { Task = "memory" }, "owner-1");

        result.Persistent.Memories.Should().Contain(m => m.Title == "Active Memory");
        result.Persistent.Memories.Should().Contain(m => m.Title == "Updated Memory");
        result.Persistent.Memories.Should().NotContain(m => m.Title == "Deleted Memory");
        result.Persistent.Memories.Should().NotContain(m => m.Title == "Superseded Memory");
        result.Persistent.Memories.Should().NotContain(m => m.Title == "Expired Memory");
    }

    // ── Owner isolation through the full pipeline ──

    [Fact]
    public async Task AssembleAsync_RealPipeline_OwnerIsolationFailsClosed()
    {
        await AddAsync(Entry("Other Owners Memory", ownerId: "owner-2"));

        var result = await _service.AssembleAsync(new UnifiedContextRequest { Task = "memory" }, "owner-1");

        result.Persistent.Memories.Should().BeEmpty();
        result.IsEmpty.Should().BeTrue();
    }

    // ── Provenance through the full pipeline ──

    [Fact]
    public async Task AssembleAsync_RealPipeline_ProvenanceSurvivesRetrieval()
    {
        var projectId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();
        await _context.MemoryEntries.AddAsync(new MemoryEntry
        {
            Id = memoryId,
            Title = "Auth Design",
            Content = "Auth Design details about config and architecture",
            Scope = MemoryScope.Project,
            State = MemoryState.Active,
            ProjectId = projectId,
            OwnerId = "owner-1",
            Source = "agent-conversation",
            MemoryType = MemoryType.ArchitectureDecision,
            Importance = 0.9,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await _service.AssembleAsync(new UnifiedContextRequest
        {
            Task = "auth",
            ProjectId = projectId
        }, "owner-1");

        var item = result.Persistent.Memories.Should().ContainSingle().Subject;
        item.MemoryId.Should().Be(memoryId);
        item.Source.Should().Be("agent-conversation");
        item.Scope.Should().Be(MemoryScope.Project);
        item.ProjectId.Should().Be(projectId);
        item.MemoryType.Should().Be(MemoryType.ArchitectureDecision);
        item.EligibilityReason.Should().Contain("Project scope");
    }

    // ── Empty persistent intelligence through the full pipeline ──

    [Fact]
    public async Task AssembleAsync_RealPipeline_NoMatchingIntelligence_ReturnsEmptyContext()
    {
        await AddAsync(Entry("Some Memory About Fruit", MemoryScope.Global));

        var result = await _service.AssembleAsync(new UnifiedContextRequest
        {
            Task = "zzzz no match zzzz"
        }, "owner-1");

        result.Persistent.Memories.Should().BeEmpty();
        result.IsEmpty.Should().BeTrue();
        result.Runtime.Request.Should().Be("zzzz no match zzzz");
    }
}
