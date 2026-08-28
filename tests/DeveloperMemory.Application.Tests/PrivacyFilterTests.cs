using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace DeveloperMemory.Application.Tests;

/// <summary>
/// Tests for privacy and isolation filtering.
/// Proves that memories are only returned when the context permits it.
/// </summary>
public class PrivacyFilterTests
{
    private const string TestOwnerId = "test-owner";
    // ── Global Scope ──

    [Fact]
    public void GlobalMemory_IsAlwaysEligible()
    {
        var globalMemory = TestDataHelper.CreateMemory(scope: MemoryScope.Global);
        var request = TestDataHelper.CreateRetrievalRequest();

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy([globalMemory], request, eligibleScopes);

        results.Should().HaveCount(1);
        results[0].Memory.Id.Should().Be(globalMemory.Id);
    }

    // ── Project Scope ──

    [Fact]
    public void ProjectMemory_IsEligibleOnlyWhenProjectContextMatches()
    {
        var projectId = Guid.NewGuid();
        var projectMemory = TestDataHelper.CreateMemory(
            scope: MemoryScope.Project, projectId: projectId);
        var request = TestDataHelper.CreateRetrievalRequest(projectId: projectId);

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy([projectMemory], request, eligibleScopes);

        results.Should().HaveCount(1);
    }

    [Fact]
    public void ProjectMemory_IsIneligibleWhenNoProjectContext()
    {
        var projectId = Guid.NewGuid();
        var projectMemory = TestDataHelper.CreateMemory(
            scope: MemoryScope.Project, projectId: projectId);
        var request = TestDataHelper.CreateRetrievalRequest(projectId: null);

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy([projectMemory], request, eligibleScopes);

        results.Should().BeEmpty();
    }

    [Fact]
    public void ProjectMemory_IsIneligibleWhenDifferentProjectContext()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        var projectBMemory = TestDataHelper.CreateMemory(
            scope: MemoryScope.Project, projectId: projectB);
        var request = TestDataHelper.CreateRetrievalRequest(projectId: projectA);

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy([projectBMemory], request, eligibleScopes);

        results.Should().BeEmpty("Project B memories must not be returned for Project A context");
    }

    // ── Workspace Scope ──

    [Fact]
    public void WorkspaceMemory_IsEligibleWhenWorkspaceIdMatches()
    {
        var workspaceMemory = TestDataHelper.CreateMemory(
            scope: MemoryScope.Workspace, workspaceId: "ws-1");
        var request = TestDataHelper.CreateRetrievalRequest(workspaceId: "ws-1");

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy([workspaceMemory], request, eligibleScopes);

        results.Should().HaveCount(1);
    }

    [Fact]
    public void WorkspaceMemory_IsIneligibleWhenWorkspaceIdMismatches()
    {
        var workspaceMemory = TestDataHelper.CreateMemory(
            scope: MemoryScope.Workspace, workspaceId: "ws-A");
        var request = TestDataHelper.CreateRetrievalRequest(workspaceId: "ws-B");

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy([workspaceMemory], request, eligibleScopes);

        results.Should().BeEmpty("Workspace A memory must not be returned for Workspace B context");
    }

    [Fact]
    public void WorkspaceMemory_IsIneligibleWhenNoWorkspaceContext()
    {
        var workspaceMemory = TestDataHelper.CreateMemory(
            scope: MemoryScope.Workspace, workspaceId: "ws-1");
        var request = TestDataHelper.CreateRetrievalRequest(workspaceId: null);

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy([workspaceMemory], request, eligibleScopes);

        results.Should().BeEmpty("Workspace memories must not be returned without workspace context");
    }

    [Fact]
    public void WorkspaceMemory_WithNullStoredWorkspaceId_MatchesAnyWorkspaceContext()
    {
        // Legacy workspace memories without WorkspaceId are returned if workspace context is present
        var legacyMemory = TestDataHelper.CreateMemory(
            scope: MemoryScope.Workspace, workspaceId: null);
        var request = TestDataHelper.CreateRetrievalRequest(workspaceId: "ws-1");

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy([legacyMemory], request, eligibleScopes);

        results.Should().HaveCount(1,
            "Legacy workspace memories without stored WorkspaceId should be returned when workspace context is present");
    }

    [Fact]
    public void CrossWorkspaceIsolation_WorkspaceAMemoryNotReturnedForWorkspaceB()
    {
        var memoryA = TestDataHelper.CreateMemory(
            title: "Workspace A Secret", scope: MemoryScope.Workspace, workspaceId: "ws-A");
        var memoryB = TestDataHelper.CreateMemory(
            title: "Workspace B Secret", scope: MemoryScope.Workspace, workspaceId: "ws-B");
        var globalMemory = TestDataHelper.CreateMemory(
            title: "Global Memory", scope: MemoryScope.Global);

        var request = TestDataHelper.CreateRetrievalRequest(workspaceId: "ws-A");

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy(
            [memoryA, memoryB, globalMemory], request, eligibleScopes);

        results.Should().HaveCount(2, "Workspace A and Global memories should be eligible");
        results.Select(r => r.Memory.Title).Should().Contain("Workspace A Secret");
        results.Select(r => r.Memory.Title).Should().Contain("Global Memory");
        results.Select(r => r.Memory.Title).Should().NotContain("Workspace B Secret");
    }

    // ── Private/User Scope ──

    [Fact]
    public void PrivateMemory_IsEligibleWhenUserIdMatches()
    {
        var privateMemory = TestDataHelper.CreateMemory(
            scope: MemoryScope.Private, userId: "user-1");
        var request = TestDataHelper.CreateRetrievalRequest(userId: "user-1");

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy([privateMemory], request, eligibleScopes);

        results.Should().HaveCount(1);
    }

    [Fact]
    public void PrivateMemory_IsIneligibleWhenUserIdMismatches()
    {
        var privateMemory = TestDataHelper.CreateMemory(
            scope: MemoryScope.Private, userId: "user-A");
        var request = TestDataHelper.CreateRetrievalRequest(userId: "user-B");

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy([privateMemory], request, eligibleScopes);

        results.Should().BeEmpty("User A's private memory must not be returned for User B");
    }

    [Fact]
    public void PrivateMemory_IsIneligibleWhenNoUserId()
    {
        var privateMemory = TestDataHelper.CreateMemory(
            scope: MemoryScope.Private, userId: "user-1");
        var request = TestDataHelper.CreateRetrievalRequest(userId: string.Empty);

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy([privateMemory], request, eligibleScopes);

        results.Should().BeEmpty("Private memories must not be returned without user context");
    }

    [Fact]
    public void PrivateMemory_WithNullStoredUserId_MatchesAnyUserContext()
    {
        // Legacy private memories without UserId are returned if user context is present
        var legacyMemory = TestDataHelper.CreateMemory(
            scope: MemoryScope.Private, userId: null);
        var request = TestDataHelper.CreateRetrievalRequest(userId: "user-1");

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy([legacyMemory], request, eligibleScopes);

        results.Should().HaveCount(1,
            "Legacy private memories without stored UserId should be returned when user context is present");
    }

    [Fact]
    public void CrossUserIsolation_UserAMemoryNotReturnedForUserB()
    {
        var memoryA = TestDataHelper.CreateMemory(
            title: "User A Private", scope: MemoryScope.Private, userId: "user-A");
        var memoryB = TestDataHelper.CreateMemory(
            title: "User B Private", scope: MemoryScope.Private, userId: "user-B");
        var globalMemory = TestDataHelper.CreateMemory(
            title: "Global Memory", scope: MemoryScope.Global);

        var request = TestDataHelper.CreateRetrievalRequest(userId: "user-A");

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy(
            [memoryA, memoryB, globalMemory], request, eligibleScopes);

        results.Should().HaveCount(2, "User A private and Global memories should be eligible");
        results.Select(r => r.Memory.Title).Should().Contain("User A Private");
        results.Select(r => r.Memory.Title).Should().Contain("Global Memory");
        results.Select(r => r.Memory.Title).Should().NotContain("User B Private");
    }

    // ── Category Filtering ──

    [Fact]
    public void ExcludedCategory_MemoryWithExcludedTag_IsFiltered()
    {
        var secretMemory = TestDataHelper.CreateMemory(
            title: "Secret Note",
            tags: ["secret", "confidential"]);
        var request = TestDataHelper.CreateRetrievalRequest();
        request.ExcludedCategories = ["secret"];

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy([secretMemory], request, eligibleScopes);

        results.Should().BeEmpty("Memory with excluded category should be filtered out");
    }

    [Fact]
    public void RequiredCategory_MemoryWithoutRequiredTag_IsFiltered()
    {
        var generalMemory = TestDataHelper.CreateMemory(
            title: "General Note",
            tags: ["general"]);
        var request = TestDataHelper.CreateRetrievalRequest();
        request.RequiredCategories = ["important"];

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy([generalMemory], request, eligibleScopes);

        results.Should().BeEmpty("Memory without required category should be filtered out");
    }

    [Fact]
    public void RequiredCategory_MemoryWithRequiredTag_IsIncluded()
    {
        var importantMemory = TestDataHelper.CreateMemory(
            title: "Important Note",
            tags: ["important"]);
        var request = TestDataHelper.CreateRetrievalRequest();
        request.RequiredCategories = ["important"];

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy([importantMemory], request, eligibleScopes);

        results.Should().HaveCount(1);
    }

    [Fact]
    public void ExplicitScopeFilter_RespectedByResolver()
    {
        var request = TestDataHelper.CreateRetrievalRequest();
        request.RequestedScopes = [MemoryScope.Global];

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);

        eligibleScopes.Should().Contain(MemoryScope.Global);
        eligibleScopes.Should().NotContain(MemoryScope.Private,
            "Private scope was not requested");
        eligibleScopes.Should().NotContain(MemoryScope.Project,
            "Project scope was not requested");
    }

    // ── Complex Cross-Scope Scenarios ──

    [Fact]
    public void CrossProjectIsolation_ProjectAMemoryNotReturnedForProjectB()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        var projectAMemory = TestDataHelper.CreateMemory(
            title: "Project A Secret",
            scope: MemoryScope.Project,
            projectId: projectA);

        var globalMemory = TestDataHelper.CreateMemory(
            title: "Global Memory",
            scope: MemoryScope.Global);

        var privateMemory = TestDataHelper.CreateMemory(
            title: "Private Memory",
            scope: MemoryScope.Private,
            userId: "user-1");

        var request = TestDataHelper.CreateRetrievalRequest(projectId: projectB, userId: "user-1");

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy(
            [projectAMemory, globalMemory, privateMemory], request, eligibleScopes);

        results.Should().HaveCount(2, "Global and Private memories should be eligible");
        results.Select(r => r.Memory.Title).Should().Contain("Global Memory");
        results.Select(r => r.Memory.Title).Should().Contain("Private Memory");
        results.Select(r => r.Memory.Title).Should().NotContain("Project A Secret");
    }

    [Fact]
    public void FullIsolation_OnlyCorrectMemoriesReturned()
    {
        // Arrange: memories from different scopes and owners
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        var memories = new List<MemoryEntry>
        {
            TestDataHelper.CreateMemory("Global", scope: MemoryScope.Global),
            TestDataHelper.CreateMemory("Project A", scope: MemoryScope.Project, projectId: projectA),
            TestDataHelper.CreateMemory("Project B", scope: MemoryScope.Project, projectId: projectB),
            TestDataHelper.CreateMemory("Workspace 1", scope: MemoryScope.Workspace, workspaceId: "ws-1"),
            TestDataHelper.CreateMemory("Workspace 2", scope: MemoryScope.Workspace, workspaceId: "ws-2"),
            TestDataHelper.CreateMemory("User 1", scope: MemoryScope.Private, userId: "user-1"),
            TestDataHelper.CreateMemory("User 2", scope: MemoryScope.Private, userId: "user-2"),
        };

        // Request from Project A, Workspace 1, User 1
        var request = TestDataHelper.CreateRetrievalRequest(
            projectId: projectA, workspaceId: "ws-1", userId: "user-1");

        var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
        var results = PrivacyFilter.FilterByPrivacy(memories, request, eligibleScopes);

        // Assert: only Global, Project A, Workspace 1, User 1 should be returned
        results.Should().HaveCount(4);
        var titles = results.Select(r => r.Memory.Title).ToList();
        titles.Should().Contain("Global");
        titles.Should().Contain("Project A");
        titles.Should().Contain("Workspace 1");
        titles.Should().Contain("User 1");
        titles.Should().NotContain("Project B");
        titles.Should().NotContain("Workspace 2");
        titles.Should().NotContain("User 2");
    }
}
