using DeveloperMemory.Application.Configuration;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Exceptions;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// V2-3: AgentRegistry — definition & resolution
// ══════════════════════════════════════════════════════════════════════════════

public class AgentRegistryTests
{
    private static AgentRegistry CreateRegistry(params AgentDefinitionOptions[] definitions)
    {
        var options = Options.Create(new AgentRegistryOptions { Agents = definitions.ToList() });
        return new AgentRegistry(options, new Mock<ILogger<AgentRegistry>>().Object);
    }

    // ── Agent definition ──

    [Fact]
    public void Registry_AlwaysProvidesBuiltInDefaultAssistant()
    {
        var registry = CreateRegistry();

        var resolution = registry.Resolve("assistant");

        resolution.Status.Should().Be(AgentResolveStatus.Resolved);
        resolution.Agent.Should().NotBeNull();
        resolution.Agent!.AgentId.Should().Be("assistant");
        resolution.Agent.Enabled.Should().BeTrue();
        resolution.Agent.SystemInstructions.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Registry_ConfiguredAgent_PreservesDefinition()
    {
        var registry = CreateRegistry(new AgentDefinitionOptions
        {
            AgentId = "writer",
            Name = "Writer",
            Description = "Writes copy.",
            SystemInstructions = "You are the writer agent.",
            Enabled = true,
            AgentType = "Documentation",
            Metadata = { ["style"] = "concise" }
        });

        var agent = registry.Resolve("writer").Agent;

        agent.Should().NotBeNull();
        agent!.Name.Should().Be("Writer");
        agent.Description.Should().Be("Writes copy.");
        agent.SystemInstructions.Should().Be("You are the writer agent.");
        agent.Enabled.Should().BeTrue();
        agent.AgentType.Should().Be(AgentType.Documentation);
        agent.Metadata["style"].Should().Be("concise");
    }

    [Fact]
    public void Registry_ConfiguredAgent_OverridesBuiltInDefault()
    {
        var registry = CreateRegistry(new AgentDefinitionOptions
        {
            AgentId = "assistant",
            Name = "Custom Assistant",
            SystemInstructions = "Custom behavior."
        });

        var agent = registry.Resolve("assistant").Agent;

        agent!.Name.Should().Be("Custom Assistant");
        agent.SystemInstructions.Should().Be("Custom behavior.");
    }

    [Fact]
    public void Registry_Resolution_IsCaseInsensitiveAndStable()
    {
        var registry = CreateRegistry(new AgentDefinitionOptions { AgentId = "Writer", Name = "Writer" });

        var first = registry.Resolve("writer").Agent;
        var second = registry.Resolve("WRITER").Agent;

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        second!.AgentId.Should().Be(first!.AgentId);
    }

    // ── Resolution behavior ──

    [Fact]
    public void Resolve_UnknownAgent_ReturnsUnknown()
    {
        var registry = CreateRegistry();

        registry.Resolve("no-such-agent").Status.Should().Be(AgentResolveStatus.Unknown);
    }

    [Fact]
    public void Resolve_NullOrEmpty_ReturnsUnknown()
    {
        var registry = CreateRegistry();

        registry.Resolve(null).Status.Should().Be(AgentResolveStatus.Unknown);
        registry.Resolve("  ").Status.Should().Be(AgentResolveStatus.Unknown);
    }

    [Fact]
    public void Resolve_DisabledAgent_ReturnsDisabled()
    {
        var registry = CreateRegistry(new AgentDefinitionOptions
        {
            AgentId = "retired",
            Name = "Retired",
            Enabled = false
        });

        var resolution = registry.Resolve("retired");

        resolution.Status.Should().Be(AgentResolveStatus.Disabled);
        resolution.Agent.Should().NotBeNull();
        resolution.Agent!.Enabled.Should().BeFalse();
    }

    [Fact]
    public void GetAll_ReturnsAllRegisteredAgents()
    {
        var registry = CreateRegistry(
            new AgentDefinitionOptions { AgentId = "writer", Name = "Writer" },
            new AgentDefinitionOptions { AgentId = "planner", Name = "Planner" });

        var all = registry.GetAll();

        all.Select(a => a.AgentId).Should().Contain(["assistant", "writer", "planner"]);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// V2-3: AssistantOrchestrator agent integration
// ══════════════════════════════════════════════════════════════════════════════

public class AssistantOrchestratorAgentTests
{
    private readonly Mock<IContextAssemblyService> _mockAssembly;
    private readonly Mock<IAssistantModelExecutor> _mockModel;
    private readonly Mock<IAgentResolver> _mockAgentResolver;
    private readonly Mock<ILogger<AssistantOrchestrator>> _mockLogger;

    public AssistantOrchestratorAgentTests()
    {
        _mockAssembly = new Mock<IContextAssemblyService>();
        // Mirrors the real ContextAssemblyService: the runtime partition echoes
        // the request; persistent intelligence is empty unless a test overrides.
        _mockAssembly.Setup(a => a.AssembleAsync(It.IsAny<UnifiedContextRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UnifiedContextRequest req, string owner, CancellationToken _) => new UnifiedAgentContext
            {
                Runtime = new RuntimeContext
                {
                    Request = req.Task,
                    Query = req.Query,
                    OwnerId = owner,
                    UserId = owner,
                    ProjectId = req.ProjectId,
                    WorkspaceId = req.WorkspaceId,
                    AgentId = req.AgentId,
                    AgentType = req.AgentType,
                    ConversationHistory = req.ConversationHistory ?? [],
                    ExplicitInstructions = req.Constraints ?? [],
                    Tags = req.Tags ?? []
                },
                Persistent = new PersistentContext(),
                Assembly = new ContextAssemblyReport()
            });

        _mockModel = new Mock<IAssistantModelExecutor>();
        _mockModel.SetupGet(m => m.IsConfigured).Returns(true);
        _mockModel.Setup(m => m.ExecuteAsync(It.IsAny<AssistantModelRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantModelResponse { Content = "agent answer", Model = "stub-model" });

        _mockAgentResolver = new Mock<IAgentResolver>();
        _mockAgentResolver.Setup(r => r.Resolve(It.IsAny<string?>()))
            .Returns(new AgentResolution { Status = AgentResolveStatus.Unknown });

        _mockLogger = new Mock<ILogger<AssistantOrchestrator>>();
    }

    private AssistantOrchestrator CreateOrchestrator() => new(
        _mockAssembly.Object, _mockModel.Object, _mockAgentResolver.Object, _mockLogger.Object);

    private void SetupResolvedAgent(Agent agent)
    {
        _mockAgentResolver.Setup(r => r.Resolve(It.IsAny<string?>()))
            .Returns(new AgentResolution { Status = AgentResolveStatus.Resolved, Agent = agent });
    }

    private static Agent WriterAgent() => new()
    {
        AgentId = "writer",
        Name = "Writer",
        Description = "Writes copy.",
        SystemInstructions = "You are the writer agent. Be concise.",
        Enabled = true
    };

    // ── Resolution before execution ──

    [Fact]
    public async Task ExecuteAsync_UnknownAgent_RejectedBeforeAnyExecution()
    {
        var act = () => CreateOrchestrator().ExecuteAsync(
            new AssistantExecutionRequest { Task = "task", AssistantId = "no-such-agent" }, "owner-1");

        await act.Should().ThrowAsync<AgentNotFoundException>();

        _mockAssembly.Verify(a => a.AssembleAsync(It.IsAny<UnifiedContextRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockModel.Verify(m => m.ExecuteAsync(It.IsAny<AssistantModelRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_DisabledAgent_RejectedBeforeAnyExecution()
    {
        _mockAgentResolver.Setup(r => r.Resolve("retired"))
            .Returns(new AgentResolution
            {
                Status = AgentResolveStatus.Disabled,
                Agent = new Agent { AgentId = "retired", Name = "Retired", Enabled = false }
            });

        var act = () => CreateOrchestrator().ExecuteAsync(
            new AssistantExecutionRequest { Task = "task", AssistantId = "retired" }, "owner-1");

        await act.Should().ThrowAsync<AgentDisabledException>();

        _mockAssembly.Verify(a => a.AssembleAsync(It.IsAny<UnifiedContextRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockModel.Verify(m => m.ExecuteAsync(It.IsAny<AssistantModelRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ResolvedAgent_ResolvedBeforeAssembly()
    {
        SetupResolvedAgent(WriterAgent());

        var resolvedBefore = false;
        _mockAssembly.Setup(a => a.AssembleAsync(It.IsAny<UnifiedContextRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => resolvedBefore = _mockAgentResolver.Invocations.Count > 0)
            .ReturnsAsync(new UnifiedAgentContext
            {
                Runtime = new RuntimeContext { Request = "task", OwnerId = "owner-1", UserId = "owner-1" },
                Persistent = new PersistentContext(),
                Assembly = new ContextAssemblyReport()
            });

        await CreateOrchestrator().ExecuteAsync(
            new AssistantExecutionRequest { Task = "task", AssistantId = "writer" }, "owner-1");

        resolvedBefore.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_NoAgentId_ResolverNotConsulted()
    {
        await CreateOrchestrator().ExecuteAsync(new AssistantExecutionRequest { Task = "task" }, "owner-1");

        _mockAgentResolver.Verify(r => r.Resolve(It.IsAny<string?>()), Times.Never);
    }

    // ── Agent instructions reach the model exchange ──

    [Fact]
    public async Task ExecuteAsync_AgentInstructionsReachSystemMessage()
    {
        SetupResolvedAgent(WriterAgent());

        AssistantModelRequest? received = null;
        _mockModel.Setup(m => m.ExecuteAsync(It.IsAny<AssistantModelRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AssistantModelRequest, CancellationToken>((req, _) => received = req)
            .ReturnsAsync(new AssistantModelResponse { Content = "ok", Model = "m" });

        await CreateOrchestrator().ExecuteAsync(
            new AssistantExecutionRequest { Task = "task", AssistantId = "writer" }, "owner-1");

        var system = received!.Messages[0].Content;
        system.Should().Contain("--- Agent Instructions ---");
        system.Should().Contain("You are the writer agent. Be concise.");
        // Runtime and persistent blocks follow the agent instructions.
        system.IndexOf("Runtime Context (current execution only)", StringComparison.Ordinal)
            .Should().BeGreaterThan(system.IndexOf("--- Agent Instructions ---", StringComparison.Ordinal));
    }

    // ── Context parts remain distinguishable with an agent ──

    [Fact]
    public async Task ExecuteAsync_AgentRuntimePersistentAndUserRequest_RemainDistinguishable()
    {
        SetupResolvedAgent(WriterAgent());

        var context = new UnifiedAgentContext
        {
            Runtime = new RuntimeContext
            {
                Request = "task",
                OwnerId = "owner-1",
                UserId = "owner-1",
                WorkspaceId = "ws-1",
                AgentId = "writer",
                ExplicitInstructions = ["use British spelling"]
            },
            Persistent = new PersistentContext
            {
                Memories =
                [
                    new RetrievedMemory
                    {
                        MemoryId = Guid.NewGuid(),
                        Title = "Convention",
                        Content = "The team uses conventional commits.",
                        MemoryType = MemoryType.Instruction,
                        Scope = MemoryScope.Global,
                        State = MemoryState.Active,
                        Source = "unit-test",
                        Importance = 0.8,
                        RelevanceScore = 0.9
                    }
                ]
            },
            Assembly = new ContextAssemblyReport()
        };
        _mockAssembly.Setup(a => a.AssembleAsync(It.IsAny<UnifiedContextRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        AssistantModelRequest? received = null;
        _mockModel.Setup(m => m.ExecuteAsync(It.IsAny<AssistantModelRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AssistantModelRequest, CancellationToken>((req, _) => received = req)
            .ReturnsAsync(new AssistantModelResponse { Content = "ok", Model = "m" });

        await CreateOrchestrator().ExecuteAsync(
            new AssistantExecutionRequest { Task = "task", AssistantId = "writer" }, "owner-1");

        var system = received!.Messages[0].Content;
        var agentIdx = system.IndexOf("--- Agent Instructions ---", StringComparison.Ordinal);
        var runtimeIdx = system.IndexOf("Runtime Context (current execution only)", StringComparison.Ordinal);
        var persistentIdx = system.IndexOf("Persistent Intelligence (read-only reference data", StringComparison.Ordinal);

        agentIdx.Should().BeGreaterThanOrEqualTo(0);
        runtimeIdx.Should().BeGreaterThan(agentIdx);
        persistentIdx.Should().BeGreaterThan(runtimeIdx);

        system.Should().Contain("use British spelling");      // runtime only
        system.Should().Contain("conventional commits");      // persistent only
        // The user request stays out of the system block.
        received.Messages[^1].Role.Should().Be("user");
        received.Messages[^1].Content.Should().Be("task");
        system.Should().NotContain("task");
    }

    // ── Agent classification hint flows into assembly ──

    [Fact]
    public async Task ExecuteAsync_AgentAgentTypeForwardedToAssembly_WhenRequestOmitsIt()
    {
        var agent = WriterAgent();
        agent.AgentType = AgentType.Documentation;
        SetupResolvedAgent(agent);

        await CreateOrchestrator().ExecuteAsync(
            new AssistantExecutionRequest { Task = "task", AssistantId = "writer" }, "owner-1");

        _mockAssembly.Verify(a => a.AssembleAsync(
            It.Is<UnifiedContextRequest>(req =>
                req.AgentId == "writer" && req.AgentType == AgentType.Documentation),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RequestAgentType_WinsOverAgentDefinition()
    {
        var agent = WriterAgent();
        agent.AgentType = AgentType.Documentation;
        SetupResolvedAgent(agent);

        await CreateOrchestrator().ExecuteAsync(
            new AssistantExecutionRequest { Task = "task", AssistantId = "writer", AgentType = AgentType.Coding }, "owner-1");

        _mockAssembly.Verify(a => a.AssembleAsync(
            It.Is<UnifiedContextRequest>(req => req.AgentType == AgentType.Coding),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Result metadata ──

    [Fact]
    public async Task ExecuteAsync_ResultCarriesAgentIdentity()
    {
        SetupResolvedAgent(WriterAgent());

        var result = await CreateOrchestrator().ExecuteAsync(
            new AssistantExecutionRequest { Task = "task", AssistantId = "writer" }, "owner-1");

        result.Execution.AgentId.Should().Be("writer");
        result.Execution.AgentName.Should().Be("Writer");
        result.Response.Should().Be("agent answer");
    }

    // ── Security boundary: agent never bypasses authenticated user ──

    [Fact]
    public async Task ExecuteAsync_AgentStillBoundToAuthenticatedOwner()
    {
        SetupResolvedAgent(WriterAgent());

        await CreateOrchestrator().ExecuteAsync(
            new AssistantExecutionRequest { Task = "task", AssistantId = "writer" }, "owner-42");

        // The server-resolved owner flows into context assembly; the agent
        // cannot substitute or bypass it.
        _mockAssembly.Verify(a => a.AssembleAsync(
            It.IsAny<UnifiedContextRequest>(),
            "owner-42",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}