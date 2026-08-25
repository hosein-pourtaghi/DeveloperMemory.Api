using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace DeveloperMemory.Tests;

/// <summary>
/// Tests for scope resolution.
/// </summary>
public class ScopeResolverTests
{
    [Fact]
    public void GlobalScope_IsAlwaysEligible()
    {
        var request = TestDataHelper.CreateRetrievalRequest();
        var scopes = ScopeResolver.ResolveEligibleScopes(request);

        scopes.Should().Contain(MemoryScope.Global);
    }

    [Fact]
    public void PrivateScope_IsEligibleWhenUserIdProvided()
    {
        var request = TestDataHelper.CreateRetrievalRequest(userId: "user-1");
        var scopes = ScopeResolver.ResolveEligibleScopes(request);

        scopes.Should().Contain(MemoryScope.Private);
    }

    [Fact]
    public void PrivateScope_IsIneligibleWithoutUserId()
    {
        var request = TestDataHelper.CreateRetrievalRequest(userId: string.Empty);
        var scopes = ScopeResolver.ResolveEligibleScopes(request);

        scopes.Should().NotContain(MemoryScope.Private,
            "Private scope should not be eligible without a user context");
    }

    [Fact]
    public void ProjectScope_IsEligibleOnlyWithProjectId()
    {
        var requestWithProject = TestDataHelper.CreateRetrievalRequest(projectId: Guid.NewGuid());
        var requestWithoutProject = TestDataHelper.CreateRetrievalRequest(projectId: null);

        ScopeResolver.ResolveEligibleScopes(requestWithProject)
            .Should().Contain(MemoryScope.Project);

        ScopeResolver.ResolveEligibleScopes(requestWithoutProject)
            .Should().NotContain(MemoryScope.Project);
    }

    [Fact]
    public void WorkspaceScope_IsEligibleOnlyWithWorkspaceId()
    {
        var requestWithWorkspace = TestDataHelper.CreateRetrievalRequest(workspaceId: "ws-1");
        var requestWithoutWorkspace = TestDataHelper.CreateRetrievalRequest(workspaceId: null);

        ScopeResolver.ResolveEligibleScopes(requestWithWorkspace)
            .Should().Contain(MemoryScope.Workspace);

        ScopeResolver.ResolveEligibleScopes(requestWithoutWorkspace)
            .Should().NotContain(MemoryScope.Workspace);
    }

    [Fact]
    public void ExplicitRequestedScopes_AreIntersectedWithEligible()
    {
        var request = TestDataHelper.CreateRetrievalRequest(projectId: Guid.NewGuid());
        request.RequestedScopes = [MemoryScope.Global, MemoryScope.Workspace];

        var scopes = ScopeResolver.ResolveEligibleScopes(request);

        scopes.Should().Contain(MemoryScope.Global);
        scopes.Should().NotContain(MemoryScope.Project, "Project was not explicitly requested");
        scopes.Should().Contain(MemoryScope.Workspace);
    }
}
