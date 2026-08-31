using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeveloperMemory.Tests;

public class AgentContextTests
{
    private readonly IAgentContextProvider _provider = new AgentContextProvider();
    private readonly IAgentContextService _service = new AgentContextService(
        new AgentContextProvider(),
        NullLogger<AgentContextService>.Instance);

    // ──────────────────────────────────────────────────
    // AgentType values
    // ──────────────────────────────────────────────────

    [Theory]
    [InlineData(AgentType.General)]
    [InlineData(AgentType.Coding)]
    [InlineData(AgentType.Documentation)]
    [InlineData(AgentType.Planning)]
    [InlineData(AgentType.Testing)]
    [InlineData(AgentType.DevOps)]
    public void Resolve_AgentType_ReturnsExpected(AgentType agentType)
    {
        var context = _provider.Resolve(agentType: agentType);
        Assert.Equal(agentType, context.AgentType);
    }

    // ──────────────────────────────────────────────────
    // TaskIntent values
    // ──────────────────────────────────────────────────

    [Theory]
    [InlineData(TaskIntent.Implement)]
    [InlineData(TaskIntent.Debug)]
    [InlineData(TaskIntent.Architecture)]
    [InlineData(TaskIntent.MemoryCapture)]
    [InlineData(TaskIntent.Query)]
    public void Resolve_TaskIntent_ReturnsExpected(TaskIntent taskIntent)
    {
        var context = _provider.Resolve(taskIntent: taskIntent);
        Assert.Equal(taskIntent, context.TaskIntent);
    }

    // ──────────────────────────────────────────────────
    // Default values
    // ──────────────────────────────────────────────────

    [Fact]
    public void Resolve_NoParameters_UsesDefaults()
    {
        var context = _provider.Resolve();
        Assert.Equal(AgentType.General, context.AgentType);
        Assert.Equal(TaskIntent.Query, context.TaskIntent);
        Assert.Null(context.ProjectId);
        Assert.Null(context.WorkspaceId);
        Assert.Null(context.UserId);
        Assert.Equal(MemoryScope.Global, context.ResolvedScope);
    }

    // ──────────────────────────────────────────────────
    // Scope resolution
    // ──────────────────────────────────────────────────

    [Fact]
    public void Resolve_ProjectIdOnly_ScopeIsProject()
    {
        var projectId = Guid.NewGuid();
        var context = _provider.Resolve(projectId: projectId);
        Assert.Equal(MemoryScope.Project, context.ResolvedScope);
        Assert.Equal(projectId, context.ProjectId);
    }

    [Fact]
    public void Resolve_WorkspaceIdOnly_ScopeIsWorkspace()
    {
        var context = _provider.Resolve(workspaceId: "ws-abc");
        Assert.Equal(MemoryScope.Workspace, context.ResolvedScope);
        Assert.Equal("ws-abc", context.WorkspaceId);
    }

    [Fact]
    public void Resolve_UserIdOnly_ScopeIsPrivate()
    {
        var context = _provider.Resolve(userId: "user-123");
        Assert.Equal(MemoryScope.Private, context.ResolvedScope);
        Assert.Equal("user-123", context.UserId);
    }

    [Fact]
    public void Resolve_AllContextFields_ScopeIsPrivate()
    {
        // Most specific scope wins
        var context = _provider.Resolve(
            projectId: Guid.NewGuid(),
            workspaceId: "ws-abc",
            userId: "user-123");
        Assert.Equal(MemoryScope.Private, context.ResolvedScope);
    }

    [Fact]
    public void Resolve_WorkspaceAndProject_ScopeIsWorkspace()
    {
        // Workspace wins over Project
        var context = _provider.Resolve(
            projectId: Guid.NewGuid(),
            workspaceId: "ws-abc");
        Assert.Equal(MemoryScope.Workspace, context.ResolvedScope);
    }

    // ──────────────────────────────────────────────────
    // Determinism — same input = same output
    // ──────────────────────────────────────────────────

    [Fact]
    public void Resolve_SameInput_ProducesSameContext()
    {
        var projectId = Guid.NewGuid();

        var ctx1 = _provider.Resolve(
            agentType: AgentType.Coding,
            taskIntent: TaskIntent.Implement,
            projectId: projectId,
            workspaceId: "ws-abc",
            userId: "user-123");

        var ctx2 = _provider.Resolve(
            agentType: AgentType.Coding,
            taskIntent: TaskIntent.Implement,
            projectId: projectId,
            workspaceId: "ws-abc",
            userId: "user-123");

        Assert.Equal(ctx1.AgentType, ctx2.AgentType);
        Assert.Equal(ctx1.TaskIntent, ctx2.TaskIntent);
        Assert.Equal(ctx1.ProjectId, ctx2.ProjectId);
        Assert.Equal(ctx1.WorkspaceId, ctx2.WorkspaceId);
        Assert.Equal(ctx1.UserId, ctx2.UserId);
        Assert.Equal(ctx1.ResolvedScope, ctx2.ResolvedScope);
    }

    // ──────────────────────────────────────────────────
    // Explicit precedence
    // ──────────────────────────────────────────────────

    [Fact]
    public void Resolve_ExplicitValues_AreHonored()
    {
        var context = _provider.Resolve(
            agentType: AgentType.DevOps,
            taskIntent: TaskIntent.Architecture,
            isExplicit: true);

        Assert.Equal(AgentType.DevOps, context.AgentType);
        Assert.Equal(TaskIntent.Architecture, context.TaskIntent);
        Assert.True(context.IsExplicit);
    }

    [Fact]
    public void Resolve_NullAgentType_DefaultsToGeneral()
    {
        var context = _provider.Resolve(agentType: null);
        Assert.Equal(AgentType.General, context.AgentType);
    }

    [Fact]
    public void Resolve_NullTaskIntent_DefaultsToQuery()
    {
        var context = _provider.Resolve(taskIntent: null);
        Assert.Equal(TaskIntent.Query, context.TaskIntent);
    }

    // ──────────────────────────────────────────────────
    // Isolation — context fields are independent
    // ──────────────────────────────────────────────────

    [Fact]
    public void Resolve_ProjectId_DoesNotAffectWorkspaceId()
    {
        var projectId = Guid.NewGuid();
        var context = _provider.Resolve(projectId: projectId);

        Assert.Equal(projectId, context.ProjectId);
        Assert.Null(context.WorkspaceId);
        Assert.Null(context.UserId);
        Assert.Equal(MemoryScope.Project, context.ResolvedScope);
    }

    [Fact]
    public void Resolve_WorkspaceId_DoesNotAffectProjectId()
    {
        var context = _provider.Resolve(workspaceId: "ws-abc");

        Assert.Null(context.ProjectId);
        Assert.Equal("ws-abc", context.WorkspaceId);
        Assert.Null(context.UserId);
        Assert.Equal(MemoryScope.Workspace, context.ResolvedScope);
    }

    [Fact]
    public void Resolve_UserId_DoesNotAffectOtherFields()
    {
        var context = _provider.Resolve(userId: "user-456");

        Assert.Null(context.ProjectId);
        Assert.Null(context.WorkspaceId);
        Assert.Equal("user-456", context.UserId);
        Assert.Equal(MemoryScope.Private, context.ResolvedScope);
    }

    // ──────────────────────────────────────────────────
    // Validation
    // ──────────────────────────────────────────────────

    [Fact]
    public void Validate_WorkspaceScopeWithoutWorkspaceId_Throws()
    {
        // Force workspace scope but omit workspaceId
        var context = new AgentContext
        {
            ResolvedScope = MemoryScope.Workspace,
            WorkspaceId = null
        };

        var ex = Assert.Throws<Application.Exceptions.DomainException>(() => _provider.Validate(context));
        Assert.Equal("agent_context_missing_workspace", ex.ErrorCode);
    }

    [Fact]
    public void Validate_ProjectScopeWithoutProjectId_Throws()
    {
        var context = new AgentContext
        {
            ResolvedScope = MemoryScope.Project,
            ProjectId = null
        };

        var ex = Assert.Throws<Application.Exceptions.DomainException>(() => _provider.Validate(context));
        Assert.Equal("agent_context_missing_project", ex.ErrorCode);
    }

    [Fact]
    public void Validate_PrivateScopeWithoutUserId_Throws()
    {
        var context = new AgentContext
        {
            ResolvedScope = MemoryScope.Private,
            UserId = null
        };

        var ex = Assert.Throws<Application.Exceptions.DomainException>(() => _provider.Validate(context));
        Assert.Equal("agent_context_missing_user", ex.ErrorCode);
    }

    [Fact]
    public void Validate_NullContext_Throws()
    {
        var ex = Assert.Throws<Application.Exceptions.DomainException>(() => _provider.Validate(null!));
        Assert.Equal("agent_context_null", ex.ErrorCode);
    }

    [Fact]
    public void Validate_GlobalScopeWithNoContext_Succeeds()
    {
        var context = new AgentContext
        {
            ResolvedScope = MemoryScope.Global,
            ProjectId = null,
            WorkspaceId = null,
            UserId = null
        };

        // Should not throw
        _provider.Validate(context);
    }

    // ──────────────────────────────────────────────────
    // Service layer integration
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task ResolveContextAsync_ValidContext_ReturnsContext()
    {
        var context = await _service.ResolveContextAsync(
            agentType: AgentType.Coding,
            taskIntent: TaskIntent.Implement,
            projectId: Guid.NewGuid());

        Assert.Equal(AgentType.Coding, context.AgentType);
        Assert.Equal(TaskIntent.Implement, context.TaskIntent);
        Assert.Equal(MemoryScope.Project, context.ResolvedScope);
    }

    [Fact]
    public async Task ResolveContextAsync_ValidContext_DoesNotThrow()
    {
        // All nulls → Global scope → valid
        var context = await _service.ResolveContextAsync();
        Assert.Equal(MemoryScope.Global, context.ResolvedScope);
    }

    [Fact]
    public void ValidateContext_WithInvalidScope_Throws()
    {
        // Manually create an invalid context that ValidateContext will catch
        var invalidContext = new AgentContext
        {
            ResolvedScope = MemoryScope.Project,
            ProjectId = null // Invalid: Project scope requires ProjectId
        };

        var ex = Assert.Throws<Application.Exceptions.DomainException>(() =>
            _service.ValidateContext(invalidContext));
        Assert.Equal("agent_context_missing_project", ex.ErrorCode);
    }

    [Fact]
    public async Task ValidateContext_InvalidContext_Throws()
    {
        var invalidContext = new AgentContext
        {
            ResolvedScope = MemoryScope.Workspace,
            WorkspaceId = null
        };

        Assert.Throws<Application.Exceptions.DomainException>(() =>
            _service.ValidateContext(invalidContext));
    }

    [Fact]
    public async Task ResolveContextAsync_WithAllFields_ReturnsCompleteContext()
    {
        var projectId = Guid.NewGuid();
        var context = await _service.ResolveContextAsync(
            agentType: AgentType.Testing,
            taskIntent: TaskIntent.Debug,
            projectId: projectId,
            workspaceId: "ws-test",
            userId: "user-test",
            isExplicit: true);

        Assert.Equal(AgentType.Testing, context.AgentType);
        Assert.Equal(TaskIntent.Debug, context.TaskIntent);
        Assert.Equal(projectId, context.ProjectId);
        Assert.Equal("ws-test", context.WorkspaceId);
        Assert.Equal("user-test", context.UserId);
        Assert.Equal(MemoryScope.Private, context.ResolvedScope);
        Assert.True(context.IsExplicit);
    }

    // ──────────────────────────────────────────────────
    // Resolution timestamp
    // ──────────────────────────────────────────────────

    [Fact]
    public void Resolve_SetsResolvedAtTimestamp()
    {
        var before = DateTime.UtcNow;
        var context = _provider.Resolve();
        var after = DateTime.UtcNow;

        Assert.True(context.ResolvedAt >= before);
        Assert.True(context.ResolvedAt <= after);
    }
}
