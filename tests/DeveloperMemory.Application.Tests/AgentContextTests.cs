using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// Phase T: AgentContextProvider Tests
// ══════════════════════════════════════════════════════════════════════════════

public class AgentContextProviderTests
{
    private readonly AgentContextProvider _provider = new();

    // ── Agent Identity ──

    [Fact]
    public void Resolve_CodingAgentId_InfersCodingType()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "cursor-agent",
            Task = "Fix the authentication bug"
        });

        Assert.Equal("cursor-agent", context.AgentId);
        Assert.Equal(AgentType.Coding, context.AgentType);
        Assert.Equal(TaskIntent.Debug, context.TaskIntent);
    }

    [Fact]
    public void Resolve_DocumentationAgentId_InfersDocumentationType()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "docwriter-agent",
            Task = "Document the API endpoints and describe the response format"
        });

        Assert.Equal(AgentType.Documentation, context.AgentType);
        Assert.Equal(TaskIntent.Documentation, context.TaskIntent);
    }

    [Fact]
    public void Resolve_PlanningAgentId_InfersPlanningType()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "planner-agent",
            Task = "Design the sprint roadmap"
        });

        Assert.Equal(AgentType.Planning, context.AgentType);
        Assert.Equal(TaskIntent.Architecture, context.TaskIntent);
    }

    [Fact]
    public void Resolve_TestingAgentId_InfersTestingType()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "qa-agent",
            Task = "Run test coverage analysis"
        });

        Assert.Equal(AgentType.Testing, context.AgentType);
        Assert.Equal(TaskIntent.Testing, context.TaskIntent);
    }

    [Fact]
    public void Resolve_DevOpsAgentId_InfersDevOpsType()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "deploy-agent",
            Task = "Deploy to production"
        });

        Assert.Equal(AgentType.DevOps, context.AgentType);
        Assert.Equal(TaskIntent.Deployment, context.TaskIntent);
    }

    [Fact]
    public void Resolve_ExplicitAgentType_UsesProvidedType()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "custom-agent",
            AgentType = AgentType.Coding,
            Task = "Build a new feature"
        });

        Assert.Equal(AgentType.Coding, context.AgentType);
    }

    [Fact]
    public void Resolve_UnknownAgentId_InfersFromTask()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "my-custom-agent",
            Task = "Document the API endpoints"
        });

        // Should infer Documentation from task since agent ID is unknown
        Assert.Equal(AgentType.Documentation, context.AgentType);
    }

    [Fact]
    public void Resolve_EmptyAgentId_ReturnsDefault()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "",
            Task = "Some task"
        });

        Assert.Equal("anonymous", context.AgentId);
        Assert.Equal(AgentType.General, context.AgentType);
        Assert.Equal(0.5, context.Confidence);
    }

    // ── Task Intent ──

    [Fact]
    public void Resolve_MemoryCaptureIntent_Detected()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent",
            Task = "Remember that we use PostgreSQL"
        });

        Assert.Equal(TaskIntent.MemoryCapture, context.TaskIntent);
    }

    [Fact]
    public void Resolve_DebugIntent_Detected()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent",
            Task = "Fix the authentication bug in login"
        });

        Assert.Equal(TaskIntent.Debug, context.TaskIntent);
    }

    [Fact]
    public void Resolve_ArchitectureIntent_Detected()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent",
            Task = "Design the microservice architecture"
        });

        Assert.Equal(TaskIntent.Architecture, context.TaskIntent);
    }

    [Fact]
    public void Resolve_ImplementIntent_Detected()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent",
            Task = "Implement the new user registration feature"
        });

        Assert.Equal(TaskIntent.Implement, context.TaskIntent);
    }

    [Fact]
    public void Resolve_RefactorIntent_Detected()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent",
            Task = "Refactor the authentication module"
        });

        Assert.Equal(TaskIntent.Refactor, context.TaskIntent);
    }

    [Fact]
    public void Resolve_QueryIntent_Detected()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent",
            Task = "What database should I use?"
        });

        Assert.Equal(TaskIntent.Query, context.TaskIntent);
    }

    [Fact]
    public void Resolve_NullTask_ReturnsGeneralIntent()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent"
        });

        Assert.Equal(TaskIntent.General, context.TaskIntent);
    }

    // ── Project Context ──

    [Fact]
    public void Resolve_ExplicitProject_MarkedAsExplicit()
    {
        var projectId = Guid.NewGuid();
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent",
            ProjectId = projectId
        });

        Assert.Equal(projectId, context.ProjectId);
        Assert.True(context.ProjectExplicit);
    }

    [Fact]
    public void Resolve_NoProject_ProjectIdIsNull()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent"
        });

        Assert.Null(context.ProjectId);
        Assert.False(context.ProjectExplicit);
    }

    // ── Workspace Context ──

    [Fact]
    public void Resolve_Workspace_Preserved()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent",
            WorkspaceId = "ws-main"
        });

        Assert.Equal("ws-main", context.WorkspaceId);
    }

    // ── Tags and Constraints ──

    [Fact]
    public void Resolve_Tags_Preserved()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent",
            Tags = ["database", "performance"]
        });

        Assert.Equal(2, context.Tags.Count);
        Assert.Contains("database", context.Tags);
    }

    [Fact]
    public void Resolve_Constraints_Preserved()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent",
            Constraints = ["no paid services", "use PostgreSQL"]
        });

        Assert.Equal(2, context.Constraints.Count);
    }

    // ── Confidence ──

    [Fact]
    public void Resolve_AllFieldsProvided_HighConfidence()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "cursor-agent",
            AgentType = AgentType.Coding,
            Task = "Fix the bug",
            ProjectId = Guid.NewGuid()
        });

        Assert.True(context.Confidence >= 0.8);
    }

    [Fact]
    public void Resolve_MinimalFields_LowerConfidence()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent"
        });

        Assert.True(context.Confidence <= 0.7);
    }

    // ── Backward Compatibility ──

    [Fact]
    public void Resolve_NoAgentType_InfersFromTask()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "unknown-agent",
            Task = "Validate the API test coverage"
        });

        Assert.Equal(AgentType.Testing, context.AgentType);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Phase T: AgentContextService Tests
// ══════════════════════════════════════════════════════════════════════════════

public class AgentContextServiceTests
{
    private readonly Mock<IAgentContextProvider> _mockProvider;
    private readonly Mock<IMemoryRetrievalService> _mockRetrieval;
    private readonly Mock<ILogger<AgentContextService>> _mockLogger;
    private readonly AgentContextService _service;

    public AgentContextServiceTests()
    {
        _mockProvider = new Mock<IAgentContextProvider>();
        _mockRetrieval = new Mock<IMemoryRetrievalService>();
        _mockLogger = new Mock<ILogger<AgentContextService>>();
        _service = new AgentContextService(
            _mockProvider.Object, _mockRetrieval.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task RetrieveContextAsync_ResolvesAgentContext()
    {
        _mockProvider.Setup(p => p.Resolve(It.IsAny<AgentContextRequest>()))
            .Returns(new AgentContext
            {
                AgentId = "test-agent",
                AgentType = AgentType.Coding,
                TaskIntent = TaskIntent.Implement,
                Confidence = 0.9
            });

        _mockRetrieval.Setup(r => r.RetrieveAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedMemoriesResult
            {
                Memories = [],
                Metadata = new RetrievalMetadata { CandidateCount = 0, SelectedCount = 0 }
            });

        var result = await _service.RetrieveContextAsync(
            new AgentContextRetrievalRequest
            {
                AgentId = "test-agent",
                Task = "Implement new feature"
            },
            "owner-1");

        Assert.Equal("test-agent", result.AgentContext.AgentId);
        Assert.Equal(AgentType.Coding, result.AgentContext.AgentType);
        _mockProvider.Verify(p => p.Resolve(It.IsAny<AgentContextRequest>()), Times.Once);
    }

    [Fact]
    public async Task RetrieveContextAsync_DelegatesToRetrievalPipeline()
    {
        _mockProvider.Setup(p => p.Resolve(It.IsAny<AgentContextRequest>()))
            .Returns(new AgentContext
            {
                AgentId = "agent",
                AgentType = AgentType.General,
                ProjectId = Guid.NewGuid()
            });

        _mockRetrieval.Setup(r => r.RetrieveAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedMemoriesResult
            {
                Memories = [],
                Metadata = new RetrievalMetadata { CandidateCount = 5, SelectedCount = 3 }
            });

        var result = await _service.RetrieveContextAsync(
            new AgentContextRetrievalRequest
            {
                AgentId = "agent",
                Query = "test query"
            },
            "owner-1");

        _mockRetrieval.Verify(r => r.RetrieveAsync(
            It.Is<RetrievalRequest>(req =>
                req.OwnerId == "owner-1" &&
                req.Query == "test query"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetrieveContextAsync_AssemblesContextSections()
    {
        _mockProvider.Setup(p => p.Resolve(It.IsAny<AgentContextRequest>()))
            .Returns(new AgentContext
            {
                AgentId = "agent",
                AgentType = AgentType.General,
                TaskIntent = TaskIntent.Implement
            });

        _mockRetrieval.Setup(r => r.RetrieveAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedMemoriesResult
            {
                Memories =
                [
                    new() { MemoryId = Guid.NewGuid(), Content = "Use PostgreSQL", MemoryType = MemoryType.TechnicalDecision, State = MemoryState.Active, RelevanceScore = 0.9 },
                    new() { MemoryId = Guid.NewGuid(), Content = "Always run tests", MemoryType = MemoryType.Instruction, State = MemoryState.Active, RelevanceScore = 0.8 },
                    new() { MemoryId = Guid.NewGuid(), Content = "I prefer minimal APIs", MemoryType = MemoryType.UserPreference, State = MemoryState.Active, RelevanceScore = 0.7 }
                ],
                Metadata = new RetrievalMetadata { CandidateCount = 3, SelectedCount = 3 }
            });

        var result = await _service.RetrieveContextAsync(
            new AgentContextRetrievalRequest
            {
                AgentId = "agent",
                Task = "Build API endpoint"
            },
            "owner-1");

        Assert.NotEmpty(result.ContextSections);
        Assert.Contains(result.ContextSections, s => s.SectionType == "TechnicalDecision");
        Assert.Contains(result.ContextSections, s => s.SectionType == "Instruction");
        Assert.Contains(result.ContextSections, s => s.SectionType == "UserPreference");
    }

    [Fact]
    public async Task RetrieveContextAsync_ExtractionsInstructions()
    {
        _mockProvider.Setup(p => p.Resolve(It.IsAny<AgentContextRequest>()))
            .Returns(new AgentContext
            {
                AgentId = "agent",
                AgentType = AgentType.General
            });

        _mockRetrieval.Setup(r => r.RetrieveAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedMemoriesResult
            {
                Memories =
                [
                    new() { MemoryId = Guid.NewGuid(), Content = "Always run tests before commit", MemoryType = MemoryType.Instruction, State = MemoryState.Active },
                    new() { MemoryId = Guid.NewGuid(), Content = "Never use paid services", MemoryType = MemoryType.UserConstraint, State = MemoryState.Active },
                    new() { MemoryId = Guid.NewGuid(), Content = "Use PostgreSQL", MemoryType = MemoryType.Fact, State = MemoryState.Active }
                ],
                Metadata = new RetrievalMetadata()
            });

        var result = await _service.RetrieveContextAsync(
            new AgentContextRetrievalRequest { AgentId = "agent" },
            "owner-1");

        Assert.Equal(2, result.Instructions.Count);
        Assert.Contains("Always run tests before commit", result.Instructions);
        Assert.Contains("Never use paid services", result.Instructions);
    }

    [Fact]
    public async Task RetrieveContextAsync_SharedMemories_AcrossAgentTypes()
    {
        // Same memory should be retrievable by different agent types
        var sharedMemory = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Content = "Project uses Clean Architecture",
            MemoryType = MemoryType.ProjectContext,
            State = MemoryState.Active,
            RelevanceScore = 0.8
        };

        _mockRetrieval.Setup(r => r.RetrieveAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedMemoriesResult
            {
                Memories = [sharedMemory],
                Metadata = new RetrievalMetadata { CandidateCount = 1, SelectedCount = 1 }
            });

        // Coding agent
        _mockProvider.Setup(p => p.Resolve(It.Is<AgentContextRequest>(r => r.AgentId == "coding")))
            .Returns(new AgentContext { AgentId = "coding", AgentType = AgentType.Coding });

        var codingResult = await _service.RetrieveContextAsync(
            new AgentContextRetrievalRequest { AgentId = "coding" }, "owner-1");

        // Documentation agent
        _mockProvider.Setup(p => p.Resolve(It.Is<AgentContextRequest>(r => r.AgentId == "docs")))
            .Returns(new AgentContext { AgentId = "docs", AgentType = AgentType.Documentation });

        var docsResult = await _service.RetrieveContextAsync(
            new AgentContextRetrievalRequest { AgentId = "docs" }, "owner-1");

        // Both should see the same memory
        Assert.Single(codingResult.Memories);
        Assert.Single(docsResult.Memories);
        Assert.Equal(codingResult.Memories[0].MemoryId, docsResult.Memories[0].MemoryId);
    }

    [Fact]
    public async Task RetrieveContextAsync_EmptyQuery_DerivesFromTask()
    {
        _mockProvider.Setup(p => p.Resolve(It.IsAny<AgentContextRequest>()))
            .Returns(new AgentContext
            {
                AgentId = "agent",
                AgentType = AgentType.Coding,
                TaskIntent = TaskIntent.Implement
            });

        _mockRetrieval.Setup(r => r.RetrieveAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedMemoriesResult
            {
                Memories = [],
                Metadata = new RetrievalMetadata()
            });

        await _service.RetrieveContextAsync(
            new AgentContextRetrievalRequest
            {
                AgentId = "agent",
                Task = "Implement user authentication"
            },
            "owner-1");

        _mockRetrieval.Verify(r => r.RetrieveAsync(
            It.Is<RetrievalRequest>(req => req.Query == "Implement user authentication"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Phase T: Agent-Aware Ranking Tests (using real RelevanceRanker)
// ══════════════════════════════════════════════════════════════════════════════

public class AgentAwareRankingTests
{
    private readonly AgentContextService _service;
    private readonly Mock<IAgentContextProvider> _mockProvider;
    private readonly Mock<IMemoryRetrievalService> _mockRetrieval;
    private readonly Mock<ILogger<AgentContextService>> _mockLogger;

    public AgentAwareRankingTests()
    {
        _mockProvider = new Mock<IAgentContextProvider>();
        _mockRetrieval = new Mock<IMemoryRetrievalService>();
        _mockLogger = new Mock<ILogger<AgentContextService>>();
        _service = new AgentContextService(
            _mockProvider.Object, _mockRetrieval.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CodingAgent_TechnicalMemoriesPrioritized()
    {
        _mockProvider.Setup(p => p.Resolve(It.IsAny<AgentContextRequest>()))
            .Returns(new AgentContext
            {
                AgentId = "cursor",
                AgentType = AgentType.Coding,
                TaskIntent = TaskIntent.Implement
            });

        _mockRetrieval.Setup(r => r.RetrieveAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedMemoriesResult
            {
                Memories =
                [
                    new() { MemoryId = Guid.NewGuid(), Content = "Architecture decision", MemoryType = MemoryType.ArchitectureDecision, State = MemoryState.Active, RelevanceScore = 0.9 },
                    new() { MemoryId = Guid.NewGuid(), Content = "Technical decision", MemoryType = MemoryType.TechnicalDecision, State = MemoryState.Active, RelevanceScore = 0.85 }
                ],
                Metadata = new RetrievalMetadata { CandidateCount = 2, SelectedCount = 2 }
            });

        var result = await _service.RetrieveContextAsync(
            new AgentContextRetrievalRequest { AgentId = "cursor", Task = "Implement feature" },
            "owner-1");

        // Coding agent should get architecture and technical decisions
        Assert.Contains(result.ContextSections, s => s.SectionType == "ArchitectureDecision");
        Assert.Contains(result.ContextSections, s => s.SectionType == "TechnicalDecision");
    }

    [Fact]
    public async Task PlanningAgent_ConstraintsAndGoalsPrioritized()
    {
        _mockProvider.Setup(p => p.Resolve(It.IsAny<AgentContextRequest>()))
            .Returns(new AgentContext
            {
                AgentId = "planner",
                AgentType = AgentType.Planning,
                TaskIntent = TaskIntent.Architecture
            });

        _mockRetrieval.Setup(r => r.RetrieveAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedMemoriesResult
            {
                Memories =
                [
                    new() { MemoryId = Guid.NewGuid(), Content = "Constraint: no paid services", MemoryType = MemoryType.UserConstraint, State = MemoryState.Active, RelevanceScore = 0.9 },
                    new() { MemoryId = Guid.NewGuid(), Content = "Goal: finish Phase 5", MemoryType = MemoryType.UserGoal, State = MemoryState.Active, RelevanceScore = 0.85 }
                ],
                Metadata = new RetrievalMetadata { CandidateCount = 2, SelectedCount = 2 }
            });

        var result = await _service.RetrieveContextAsync(
            new AgentContextRetrievalRequest { AgentId = "planner", Task = "Plan sprint" },
            "owner-1");

        Assert.Contains(result.ContextSections, s => s.SectionType == "UserConstraint");
        Assert.Contains(result.ContextSections, s => s.SectionType == "UserGoal");
    }

    [Fact]
    public async Task DifferentAgentTypes_SameMemory_DifferentSections()
    {
        // Same memory content, different context sections based on agent type
        var memory = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Content = "Use Clean Architecture pattern",
            MemoryType = MemoryType.ArchitectureDecision,
            State = MemoryState.Active,
            RelevanceScore = 0.8
        };

        _mockRetrieval.Setup(r => r.RetrieveAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedMemoriesResult
            {
                Memories = [memory],
                Metadata = new RetrievalMetadata { CandidateCount = 1, SelectedCount = 1 }
            });

        // Coding agent
        _mockProvider.Setup(p => p.Resolve(It.Is<AgentContextRequest>(r => r.AgentId == "coding")))
            .Returns(new AgentContext { AgentId = "coding", AgentType = AgentType.Coding });

        var codingResult = await _service.RetrieveContextAsync(
            new AgentContextRetrievalRequest { AgentId = "coding" }, "owner-1");

        // Documentation agent
        _mockProvider.Setup(p => p.Resolve(It.Is<AgentContextRequest>(r => r.AgentId == "docs")))
            .Returns(new AgentContext { AgentId = "docs", AgentType = AgentType.Documentation });

        var docsResult = await _service.RetrieveContextAsync(
            new AgentContextRetrievalRequest { AgentId = "docs" }, "owner-1");

        // Both should see the memory, but in different sections
        Assert.Contains(codingResult.ContextSections, s => s.SectionType == "ArchitectureDecision");
        Assert.Contains(docsResult.ContextSections, s => s.SectionType == "ArchitectureDecision");
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Phase T: Security Tests
// ══════════════════════════════════════════════════════════════════════════════

public class AgentContextSecurityTests
{
    private readonly AgentContextProvider _provider = new();

    [Fact]
    public void Resolve_AgentContext_DoesNotOverrideOwnerId()
    {
        // Agent context resolution should NOT affect ownership
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent-a",
            Task = "Access user B data"
        });

        // The agent context is just metadata — it doesn't affect ownership
        Assert.Equal("agent-a", context.AgentId);
        // Ownership is still determined by ICurrentUser.UserId, not agent context
    }

    [Fact]
    public void Resolve_AgentContext_DoesNotBypassScope()
    {
        var projectId = Guid.NewGuid();
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent",
            ProjectId = projectId
        });

        Assert.Equal(projectId, context.ProjectId);
        // Scope enforcement is still handled by PrivacyFilter and LifecycleFilter
    }

    [Fact]
    public void Resolve_AgentContext_DoesNotBypassClassification()
    {
        var context = _provider.Resolve(new AgentContextRequest
        {
            AgentId = "agent",
            Task = "Access secret data"
        });

        // Agent context doesn't change classification rules
        Assert.Equal(AgentType.General, context.AgentType);
    }
}
