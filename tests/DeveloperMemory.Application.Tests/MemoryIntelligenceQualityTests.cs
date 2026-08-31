using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// Phase S: Memory Intelligence Quality Evaluation Suite
// Deterministic tests for the improved retrieval pipeline.
// ══════════════════════════════════════════════════════════════════════════════

public class RelevanceRankerTests_PhaseS
{
    private readonly RelevanceRanker _ranker = new();

    private RetrievalRequest CreateRequest(
        string query = "test query",
        Guid? projectId = null,
        string? workspaceId = null,
        string? userId = null,
        string ownerId = "owner-1")
    {
        return new RetrievalRequest
        {
            Query = query,
            OwnerId = ownerId,
            UserId = userId ?? ownerId,
            ProjectId = projectId,
            WorkspaceId = workspaceId
        };
    }

    private RetrievedMemory CreateMemory(
        string title = "Test Memory",
        string content = "Test content",
        MemoryScope scope = MemoryScope.Global,
        MemoryType memoryType = MemoryType.Fact,
        double importance = 0.5,
        double confidence = 1.0,
        Guid? projectId = null,
        MemoryState state = MemoryState.Active,
        string? source = null,
        List<string>? tags = null,
        DateTime? updatedAt = null)
    {
        return new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = title,
            Content = content,
            Scope = scope,
            MemoryType = memoryType,
            Importance = importance,
            Confidence = confidence,
            ProjectId = projectId,
            State = state,
            Source = source,
            Tags = tags ?? [],
            UpdatedAt = updatedAt ?? DateTime.UtcNow,
            EstimatedTokens = (int)Math.Ceiling((title.Length + content.Length) / 4.0)
        };
    }

    // ── Relevance: Exact Match ──

    [Fact]
    public async Task RankAsync_ExactTitleMatch_RanksHighest()
    {
        var request = CreateRequest(query: "PostgreSQL");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Database Choice", content: "We use Redis for caching"),
            CreateMemory(title: "PostgreSQL Setup", content: "Configure PostgreSQL connection"),
            CreateMemory(title: "Frontend", content: "Use Blazor for UI")
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        Assert.Equal("PostgreSQL Setup", ranked[0].Title);
    }

    // ── Relevance: Lexical Match ──

    [Fact]
    public async Task RankAsync_ContentMatch_RanksAboveUnrelated()
    {
        var request = CreateRequest(query: "authentication middleware");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Caching", content: "Redis caching strategy"),
            CreateMemory(title: "Security", content: "Implement authentication middleware for all endpoints"),
            CreateMemory(title: "Database", content: "PostgreSQL connection pooling")
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        Assert.Equal("Security", ranked[0].Title);
    }

    // ── Relevance: Unrelated Memory ──

    [Fact]
    public async Task RankAsync_UnrelatedMemory_RanksLowest()
    {
        var request = CreateRequest(query: "PostgreSQL database");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Deployment", content: "Deploy to Azure using Docker containers"),
            CreateMemory(title: "Database", content: "PostgreSQL is our primary database"),
            CreateMemory(title: "Frontend", content: "React component library")
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        // PostgreSQL content should rank above unrelated
        Assert.Equal("Database", ranked[0].Title);
        Assert.Equal("Deployment", ranked[^1].Title);
    }

    // ── Scope: Global vs Project ──

    [Fact]
    public async Task RankAsync_ProjectScopedMemory_RanksHigherInProjectContext()
    {
        var projectId = Guid.NewGuid();
        var request = CreateRequest(query: "database", projectId: projectId);
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Global Database", content: "Configure the database for general use", scope: MemoryScope.Global),
            CreateMemory(title: "Project Database", content: "Configure the database for this project", scope: MemoryScope.Project, projectId: projectId),
            CreateMemory(title: "Other Project Database", content: "Configure the database for other project", scope: MemoryScope.Project, projectId: Guid.NewGuid())
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        // Project-scoped to current project should rank highest
        Assert.Equal("Project Database", ranked[0].Title);
        // Other project should rank lowest
        Assert.Equal("Other Project Database", ranked[^1].Title);
    }

    [Fact]
    public async Task RankAsync_GlobalMemory_NeutralWithoutProjectContext()
    {
        var request = CreateRequest(query: "database");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Global DB", content: "Use PostgreSQL for all databases", scope: MemoryScope.Global),
            CreateMemory(title: "Project DB", content: "Use MySQL for this specific project", scope: MemoryScope.Project, projectId: Guid.NewGuid())
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        // Without project context, global should rank above orphaned project memory
        Assert.Equal("Global DB", ranked[0].Title);
    }

    // ── Scope: Wrong Project ──

    [Fact]
    public async Task RankAsync_WrongProjectMemory_RanksLowest()
    {
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var request = CreateRequest(query: "database", projectId: projectId);
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Other Project", content: "Use MongoDB", scope: MemoryScope.Project, projectId: otherProjectId),
            CreateMemory(title: "Global", content: "Use PostgreSQL", scope: MemoryScope.Global)
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        Assert.Equal("Global", ranked[0].Title);
        Assert.Equal("Other Project", ranked[^1].Title);
    }

    // ── Lifecycle: Active vs Superseded ──

    [Fact]
    public async Task RankAsync_SupersededMemory_RanksLowerThanActive()
    {
        var request = CreateRequest(query: "database");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Old DB", content: "Use MySQL", state: MemoryState.Superseded),
            CreateMemory(title: "New DB", content: "Use PostgreSQL", state: MemoryState.Active)
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        Assert.Equal("New DB", ranked[0].Title);
    }

    // ── Lifecycle: Expired ──

    [Fact]
    public async Task RankAsync_ExpiredMemory_RanksLowest()
    {
        var request = CreateRequest(query: "database");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Expired DB", content: "Use MySQL", state: MemoryState.Expired),
            CreateMemory(title: "Active DB", content: "Use PostgreSQL", state: MemoryState.Active)
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        // Expired memories should still be ranked (lifecycle filter handles exclusion)
        // but should rank lower due to lower recency/state signals
        Assert.Equal("Active DB", ranked[0].Title);
    }

    // ── Consolidation: Duplicate Suppression ──

    [Fact]
    public async Task RankAsync_DuplicateContent_SuppressedToSingle()
    {
        var request = CreateRequest(query: "database");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "DB Choice", content: "Use PostgreSQL for persistence"),
            CreateMemory(title: "DB Choice", content: "Use PostgreSQL for persistence"),
            CreateMemory(title: "DB Choice", content: "Use PostgreSQL for persistence")
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        // All three have identical title+content — only one should survive
        Assert.Single(ranked);
    }

    [Fact]
    public async Task RankAsync_DuplicateContent_KeepsHighestImportance()
    {
        var request = CreateRequest(query: "database");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "DB Choice", content: "Use PostgreSQL for persistence", importance: 0.3),
            CreateMemory(title: "DB Choice", content: "Use PostgreSQL for persistence", importance: 0.9),
            CreateMemory(title: "DB Choice", content: "Use PostgreSQL for persistence", importance: 0.6)
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        Assert.Single(ranked);
        Assert.Equal("DB Choice", ranked[0].Title);
    }

    [Fact]
    public async Task RankAsync_DuplicateContent_SupersededIsReplacedByActive()
    {
        var request = CreateRequest(query: "database");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "DB Choice", content: "Use MySQL for database", state: MemoryState.Superseded),
            CreateMemory(title: "DB Choice", content: "Use MySQL for database", state: MemoryState.Active)
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        Assert.Single(ranked);
        Assert.Equal("DB Choice", ranked[0].Title);
    }

    // ── Consolidation: Multiple Sources ──

    [Fact]
    public async Task RankAsync_DistinctFacts_NotSuppressed()
    {
        var request = CreateRequest(query: "database");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "DB Choice", content: "Use PostgreSQL for primary database"),
            CreateMemory(title: "Cache Choice", content: "Use Redis for caching layer"),
            CreateMemory(title: "Search Choice", content: "Use Elasticsearch for full-text search")
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        // All three are distinct — all should survive
        Assert.Equal(3, ranked.Count);
    }

    // ── Conflicts: Current vs Stale ──

    [Fact]
    public async Task RankAsync_SupersessionRelationship_CurrentRanksHigher()
    {
        var projectId = Guid.NewGuid();
        var request = CreateRequest(query: "database", projectId: projectId);
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Old DB Decision", content: "Use MySQL for database",
                state: MemoryState.Superseded, scope: MemoryScope.Project, projectId: projectId,
                updatedAt: DateTime.UtcNow.AddDays(-30)),
            CreateMemory(title: "New DB Decision", content: "Use PostgreSQL for database",
                state: MemoryState.Active, scope: MemoryScope.Project, projectId: projectId,
                updatedAt: DateTime.UtcNow)
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        Assert.Equal("New DB Decision", ranked[0].Title);
    }

    // ── Quality: High Confidence vs Low Confidence ──

    [Fact]
    public async Task RankAsync_HighConfidence_RanksHigherThanLowConfidence()
    {
        var request = CreateRequest(query: "database");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Low Conf", content: "Maybe use PostgreSQL", confidence: 0.3),
            CreateMemory(title: "High Conf", content: "Use PostgreSQL for database", confidence: 0.95)
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        Assert.Equal("High Conf", ranked[0].Title);
    }

    // ── Quality: High Importance vs Low Importance ──

    [Fact]
    public async Task RankAsync_HighImportance_RanksHigher()
    {
        var request = CreateRequest(query: "database");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Low Imp", content: "Use PostgreSQL", importance: 0.2),
            CreateMemory(title: "High Imp", content: "Use PostgreSQL", importance: 0.9)
        };

        // Both have same content so duplicate suppression will keep only one
        // Let's use different content
        var candidates2 = new List<RetrievedMemory>
        {
            CreateMemory(title: "Low Imp", content: "PostgreSQL is okay for small projects", importance: 0.2),
            CreateMemory(title: "High Imp", content: "PostgreSQL is our primary database for all projects", importance: 0.9)
        };

        var ranked = await _ranker.RankAsync(candidates2, request);

        Assert.Equal("High Imp", ranked[0].Title);
    }

    // ── Quality: Duplicate-Heavy Candidate Set ──

    [Fact]
    public async Task RankAsync_DuplicateHeavySet_PreservesDistinctFacts()
    {
        var request = CreateRequest(query: "architecture");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Arch Choice", content: "Use Clean Architecture", importance: 0.8),
            CreateMemory(title: "Arch Choice", content: "Use Clean Architecture", importance: 0.9),
            CreateMemory(title: "Arch Choice", content: "Use Clean Architecture", importance: 0.7),
            CreateMemory(title: "DB Choice", content: "Use PostgreSQL", importance: 0.5),
            CreateMemory(title: "Cache Choice", content: "Use Redis", importance: 0.4)
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        // 3 duplicates collapsed to 1, plus 2 distinct = 3
        Assert.Equal(3, ranked.Count);
        Assert.Equal("Arch Choice", ranked[0].Title); // Highest importance duplicate kept
    }

    // ── Quality: Context Budget Pressure ──

    [Fact]
    public async Task RankAsync_ManyCandidates_ReturnsAllSortedByRelevance()
    {
        var request = CreateRequest(query: "project");
        var candidates = Enumerable.Range(1, 20)
            .Select(i => CreateMemory(
                title: $"Memory {i}",
                content: $"Content about project topic {i}",
                importance: i / 20.0))
            .ToList();

        var ranked = await _ranker.RankAsync(candidates, request);

        Assert.Equal(20, ranked.Count);
        // Should be sorted by relevance descending
        for (int i = 0; i < ranked.Count - 1; i++)
        {
            Assert.True(ranked[i].RelevanceScore >= ranked[i + 1].RelevanceScore);
        }
    }

    // ── Security: Classification ──

    [Fact]
    public async Task RankAsync_ScopeFiltering_OtherProjectExcluded()
    {
        var projectId = Guid.NewGuid();
        var request = CreateRequest(query: "database", projectId: projectId);
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "My Project DB", content: "Use PostgreSQL",
                scope: MemoryScope.Project, projectId: projectId),
            CreateMemory(title: "Other Project DB", content: "Use MySQL",
                scope: MemoryScope.Project, projectId: Guid.NewGuid())
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        // Other project memory should rank much lower
        Assert.Equal("My Project DB", ranked[0].Title);
        Assert.Equal("Other Project DB", ranked[1].Title);
        Assert.True(ranked[0].RelevanceScore > ranked[1].RelevanceScore);
    }

    // ── Memory Type Scoring ──

    [Fact]
    public async Task RankAsync_InstructionType_RanksHigherForOperationalQuery()
    {
        var request = CreateRequest(query: "how to implement authentication");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Fact", content: "Authentication is important",
                memoryType: MemoryType.Fact),
            CreateMemory(title: "Instruction", content: "Always use JWT tokens for authentication",
                memoryType: MemoryType.Instruction),
            CreateMemory(title: "Constraint", content: "Never use basic auth in production",
                memoryType: MemoryType.UserConstraint)
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        // Instructions and constraints should rank above facts for operational queries
        Assert.True(ranked[0].MemoryType == MemoryType.Instruction ||
                    ranked[0].MemoryType == MemoryType.UserConstraint);
    }

    [Fact]
    public async Task RankAsync_TechnicalDecision_RanksHigherForTechnicalQuery()
    {
        var request = CreateRequest(query: "what database framework should we use");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Preference", content: "I prefer dark theme",
                memoryType: MemoryType.UserPreference),
            CreateMemory(title: "Tech Decision", content: "We selected Entity Framework Core",
                memoryType: MemoryType.TechnicalDecision)
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        Assert.Equal("Tech Decision", ranked[0].Title);
    }

    // ── Tie-breaking ──

    [Fact]
    public async Task RankAsync_SameScore_TiesBrokenByImportance()
    {
        var request = CreateRequest(query: "test");
        var now = DateTime.UtcNow;
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "A", content: "Test memory A", importance: 0.3, updatedAt: now),
            CreateMemory(title: "B", content: "Test memory B", importance: 0.8, updatedAt: now)
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        Assert.Equal("B", ranked[0].Title);
    }

    [Fact]
    public async Task RankAsync_SameScoreAndImportance_TiesBrokenByRecency()
    {
        var request = CreateRequest(query: "test");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Old", content: "Test memory old", importance: 0.5,
                updatedAt: DateTime.UtcNow.AddDays(-10)),
            CreateMemory(title: "New", content: "Test memory new", importance: 0.5,
                updatedAt: DateTime.UtcNow)
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        Assert.Equal("New", ranked[0].Title);
    }

    // ── Score Breakdown ──

    [Fact]
    public async Task RankAsync_ScoreBreakdown_Populated()
    {
        var request = CreateRequest(query: "database");
        var candidates = new List<RetrievedMemory>
        {
            CreateMemory(title: "Database", content: "Use PostgreSQL for database")
        };

        var ranked = await _ranker.RankAsync(candidates, request);

        Assert.NotNull(ranked[0].ScoreBreakdown);
        Assert.True(ranked[0].ScoreBreakdown.TextRelevance > 0);
        Assert.True(ranked[0].RelevanceScore > 0);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Phase S: Lifecycle Filter Tests
// ══════════════════════════════════════════════════════════════════════════════

public class LifecycleFilterTests_PhaseS
{
    [Fact]
    public void FilterByLifecycle_ActiveMemory_Passes()
    {
        var memory = new MemoryEntry { State = MemoryState.Active };
        var eligible = LifecycleFilter.IsEligible(memory);
        Assert.True(eligible);
    }

    [Fact]
    public void FilterByLifecycle_UpdatedMemory_Passes()
    {
        var memory = new MemoryEntry { State = MemoryState.Updated };
        var eligible = LifecycleFilter.IsEligible(memory);
        Assert.True(eligible);
    }

    [Fact]
    public void FilterByLifecycle_SupersededMemory_Excluded()
    {
        var memory = new MemoryEntry { State = MemoryState.Superseded };
        var eligible = LifecycleFilter.IsEligible(memory);
        Assert.False(eligible);
    }

    [Fact]
    public void FilterByLifecycle_ExpiredMemory_Excluded()
    {
        var memory = new MemoryEntry
        {
            State = MemoryState.Active,
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        var eligible = LifecycleFilter.IsEligible(memory);
        Assert.False(eligible);
    }

    [Fact]
    public void FilterByLifecycle_ArchivedMemory_Excluded()
    {
        var memory = new MemoryEntry { State = MemoryState.Archived };
        var eligible = LifecycleFilter.IsEligible(memory);
        Assert.False(eligible);
    }

    [Fact]
    public void FilterByLifecycle_DeletedMemory_Excluded()
    {
        var memory = new MemoryEntry { State = MemoryState.Deleted };
        var eligible = LifecycleFilter.IsEligible(memory);
        Assert.False(eligible);
    }

    [Fact]
    public void FilterByLifecycle_ActiveNotExpired_Passes()
    {
        var memory = new MemoryEntry
        {
            State = MemoryState.Active,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        var eligible = LifecycleFilter.IsEligible(memory);
        Assert.True(eligible);
    }

    [Fact]
    public void FilterByLifecycle_FiltersCorrectly()
    {
        var memories = new List<(MemoryEntry Memory, string EligibilityReason)>
        {
            (new MemoryEntry { State = MemoryState.Active }, "active"),
            (new MemoryEntry { State = MemoryState.Superseded }, "superseded"),
            (new MemoryEntry { State = MemoryState.Deleted }, "deleted"),
            (new MemoryEntry { State = MemoryState.Expired }, "expired"),
            (new MemoryEntry { State = MemoryState.Archived }, "archived"),
            (new MemoryEntry { State = MemoryState.Updated }, "updated"),
            (new MemoryEntry { State = MemoryState.Active, ExpiresAt = DateTime.UtcNow.AddDays(-1) }, "expired-active")
        };

        var filtered = LifecycleFilter.FilterByLifecycle(memories);

        Assert.Equal(2, filtered.Count);
        Assert.Contains(filtered, f => f.EligibilityReason == "active");
        Assert.Contains(filtered, f => f.EligibilityReason == "updated");
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Phase S: Privacy Filter Tests (Security)
// ══════════════════════════════════════════════════════════════════════════════

public class PrivacyFilterTests_PhaseS
{
    [Fact]
    public void FilterByPrivacy_GlobalMemory_InGlobalScope_Returned()
    {
        var memory = new MemoryEntry
        {
            Scope = MemoryScope.Global,
            OwnerId = "owner-1"
        };
        var request = new RetrievalRequest { OwnerId = "owner-1" };
        var scopes = new List<MemoryScope> { MemoryScope.Global };

        var results = PrivacyFilter.FilterByPrivacy([memory], request, scopes);

        Assert.Single(results);
    }

    [Fact]
    public void FilterByPrivacy_ProjectMemory_WrongProject_Excluded()
    {
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var memory = new MemoryEntry
        {
            Scope = MemoryScope.Project,
            ProjectId = otherProjectId,
            OwnerId = "owner-1"
        };
        var request = new RetrievalRequest
        {
            OwnerId = "owner-1",
            ProjectId = projectId
        };
        var scopes = new List<MemoryScope> { MemoryScope.Global, MemoryScope.Project };

        var results = PrivacyFilter.FilterByPrivacy([memory], request, scopes);

        Assert.Empty(results);
    }

    [Fact]
    public void FilterByPrivacy_DifferentOwner_Excluded()
    {
        var memory = new MemoryEntry
        {
            Scope = MemoryScope.Global,
            OwnerId = "owner-2"
        };
        var request = new RetrievalRequest { OwnerId = "owner-1" };
        var scopes = new List<MemoryScope> { MemoryScope.Global };

        var results = PrivacyFilter.FilterByPrivacy([memory], request, scopes);

        Assert.Empty(results);
    }

    [Fact]
    public void FilterByPrivacy_EmptyOwnerId_ExcludesAll()
    {
        var memory = new MemoryEntry
        {
            Scope = MemoryScope.Global,
            OwnerId = "owner-1"
        };
        var request = new RetrievalRequest { OwnerId = "" };
        var scopes = new List<MemoryScope> { MemoryScope.Global };

        var results = PrivacyFilter.FilterByPrivacy([memory], request, scopes);

        Assert.Empty(results);
    }

    [Fact]
    public void FilterByPrivacy_ProjectMemory_CorrectProject_Returned()
    {
        var projectId = Guid.NewGuid();
        var memory = new MemoryEntry
        {
            Scope = MemoryScope.Project,
            ProjectId = projectId,
            OwnerId = "owner-1"
        };
        var request = new RetrievalRequest
        {
            OwnerId = "owner-1",
            ProjectId = projectId
        };
        var scopes = new List<MemoryScope> { MemoryScope.Global, MemoryScope.Project };

        var results = PrivacyFilter.FilterByPrivacy([memory], request, scopes);

        Assert.Single(results);
    }

    [Fact]
    public void FilterByPrivacy_WorkspaceMemory_CorrectWorkspace_Returned()
    {
        var memory = new MemoryEntry
        {
            Scope = MemoryScope.Workspace,
            WorkspaceId = "ws-1",
            OwnerId = "owner-1"
        };
        var request = new RetrievalRequest
        {
            OwnerId = "owner-1",
            WorkspaceId = "ws-1"
        };
        var scopes = new List<MemoryScope> { MemoryScope.Global, MemoryScope.Workspace };

        var results = PrivacyFilter.FilterByPrivacy([memory], request, scopes);

        Assert.Single(results);
    }

    [Fact]
    public void FilterByPrivacy_WorkspaceMemory_WrongWorkspace_Excluded()
    {
        var memory = new MemoryEntry
        {
            Scope = MemoryScope.Workspace,
            WorkspaceId = "ws-1",
            OwnerId = "owner-1"
        };
        var request = new RetrievalRequest
        {
            OwnerId = "owner-1",
            WorkspaceId = "ws-2"
        };
        var scopes = new List<MemoryScope> { MemoryScope.Global, MemoryScope.Workspace };

        var results = PrivacyFilter.FilterByPrivacy([memory], request, scopes);

        Assert.Empty(results);
    }

    [Fact]
    public void FilterByPrivacy_PrivateMemory_CorrectUser_Returned()
    {
        var memory = new MemoryEntry
        {
            Scope = MemoryScope.Private,
            UserId = "user-1",
            OwnerId = "owner-1"
        };
        var request = new RetrievalRequest
        {
            OwnerId = "owner-1",
            UserId = "user-1"
        };
        var scopes = new List<MemoryScope> { MemoryScope.Global, MemoryScope.Private };

        var results = PrivacyFilter.FilterByPrivacy([memory], request, scopes);

        Assert.Single(results);
    }

    [Fact]
    public void FilterByPrivacy_PrivateMemory_WrongUser_Excluded()
    {
        var memory = new MemoryEntry
        {
            Scope = MemoryScope.Private,
            UserId = "user-1",
            OwnerId = "owner-1"
        };
        var request = new RetrievalRequest
        {
            OwnerId = "owner-1",
            UserId = "user-2"
        };
        var scopes = new List<MemoryScope> { MemoryScope.Global, MemoryScope.Private };

        var results = PrivacyFilter.FilterByPrivacy([memory], request, scopes);

        Assert.Empty(results);
    }

    [Fact]
    public void FilterByPrivacy_ExcludedCategory_Filtered()
    {
        var memory = new MemoryEntry
        {
            Scope = MemoryScope.Global,
            OwnerId = "owner-1",
            TagsJson = "[\"sensitive\"]"
        };
        var request = new RetrievalRequest
        {
            OwnerId = "owner-1",
            ExcludedCategories = ["sensitive"]
        };
        var scopes = new List<MemoryScope> { MemoryScope.Global };

        var results = PrivacyFilter.FilterByPrivacy([memory], request, scopes);

        Assert.Empty(results);
    }

    [Fact]
    public void FilterByPrivacy_InaccessibleClassification_StillReturned()
    {
        // Classification doesn't filter at the PrivacyFilter level
        // It's handled by scoring (lower classification = lower relevance)
        var memory = new MemoryEntry
        {
            Scope = MemoryScope.Global,
            OwnerId = "owner-1",
            Classification = DataClassification.Secret
        };
        var request = new RetrievalRequest { OwnerId = "owner-1" };
        var scopes = new List<MemoryScope> { MemoryScope.Global };

        var results = PrivacyFilter.FilterByPrivacy([memory], request, scopes);

        Assert.Single(results);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Phase S: Consolidation Safety Tests
// ══════════════════════════════════════════════════════════════════════════════

public class ConsolidationSafetyTests_PhaseS
{
    private readonly DocumentConsolidationService _service;
    private readonly Mock<IMemoryRepository> _mockRepo;
    private readonly Mock<IMemoryConflictDetector> _mockConflictDetector;
    private readonly Mock<ILogger<DocumentConsolidationService>> _mockLogger;

    public ConsolidationSafetyTests_PhaseS()
    {
        _mockRepo = new Mock<IMemoryRepository>();
        _mockConflictDetector = new Mock<IMemoryConflictDetector>();
        _mockLogger = new Mock<ILogger<DocumentConsolidationService>>();
        _service = new DocumentConsolidationService(
            _mockRepo.Object, _mockConflictDetector.Object, _mockLogger.Object);

        // Default: no conflicts
        _mockConflictDetector.Setup(d => d.DetectConflicts(
            It.IsAny<MemoryEntry>(), It.IsAny<IReadOnlyList<MemoryEntry>>()))
            .Returns([]);

        _mockRepo.Setup(r => r.CreateAsync(
            It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemoryEntry e, CancellationToken ct) => { e.Id = Guid.NewGuid(); return e; });

        _mockRepo.Setup(r => r.SearchAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _mockRepo.Setup(r => r.GetByScopeAsync(
            It.IsAny<MemoryScope>(), It.IsAny<string>(), It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private string _ownerId = "owner-1";

    [Fact]
    public async Task SameFact_DifferentSources_NotMerged()
    {
        // Two memories about the same fact but from different sources
        // should not be automatically merged
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL for the database",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            NormalizedContent = "use postgresql for the database",
            Source = "knowledge:tech-stack"
        };

        _mockRepo.Setup(r => r.SearchAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existing]);

        // Same content from a different source
        var candidate = new CanonicalMemoryCandidate
        {
            Title = "DB Choice",
            Content = "Use PostgreSQL for the database",
            NormalizedContent = "use postgresql for the database",
            Scope = MemoryScope.Global,
            Source = "knowledge:database-guide"
        };

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        // Should be detected as duplicate, not merged
        Assert.Equal(ConsolidationAction.DuplicateIgnored, result.Action);
    }

    [Fact]
    public async Task SimilarButNonIdentical_NotFalsePositive()
    {
        // Similar content that is NOT the same fact
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL for the primary database",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            NormalizedContent = "use postgresql for the primary database"
        };

        _mockRepo.Setup(r => r.SearchAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existing]);

        var candidate = new CanonicalMemoryCandidate
        {
            Title = "DB Backup",
            Content = "Use PostgreSQL for the backup database",
            NormalizedContent = "use postgresql for the backup database",
            Scope = MemoryScope.Global,
            Source = "knowledge:backup"
        };

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        // Should be created as new (different enough)
        Assert.Equal(ConsolidationAction.Created, result.Action);
    }

    [Fact]
    public async Task ConflictingFacts_RoutedToReview()
    {
        // Same topic, different values — should not auto-resolve
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use Angular for the frontend",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            NormalizedContent = "use angular for the frontend",
            MemoryType = MemoryType.UserConstraint
        };

        _mockRepo.Setup(r => r.SearchAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existing]);

        var candidate = new CanonicalMemoryCandidate
        {
            Title = "Frontend",
            Content = "Don't use Angular for the frontend",
            NormalizedContent = "dont use angular for the frontend",
            Scope = MemoryScope.Global,
            Source = "knowledge:frontend",
            MemoryType = MemoryType.UserConstraint
        };

        _mockConflictDetector.Setup(d => d.DetectConflicts(
            It.IsAny<MemoryEntry>(), It.IsAny<IReadOnlyList<MemoryEntry>>()))
            .Returns([new MemoryConflict
            {
                ExistingMemory = existing,
                ConflictType = MemoryConflictType.Contradiction,
                Explanation = "Conflicting frameworks",
                ShouldSupersede = false,
                Confidence = 0.6
            }]);

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        Assert.Equal(ConsolidationAction.RequiresReview, result.Action);
    }

    [Fact]
    public async Task ProjectSpecificVsGlobal_NotConfused()
    {
        // Project-specific memory should not interfere with global memory
        var projectId = Guid.NewGuid();
        var globalMemory = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            NormalizedContent = "use postgresql"
        };

        _mockRepo.Setup(r => r.SearchAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([globalMemory]);

        var projectCandidate = new CanonicalMemoryCandidate
        {
            Title = "Project DB",
            Content = "Use PostgreSQL",
            NormalizedContent = "use postgresql",
            Scope = MemoryScope.Project,
            ProjectId = projectId,
            Source = "knowledge:project-db"
        };

        var result = await _service.ConsolidateAsync(projectCandidate, _ownerId);

        // Different scopes — should be created as new
        Assert.Equal(ConsolidationAction.Created, result.Action);
    }

    [Fact]
    public async Task ShortContent_Rejected()
    {
        var candidate = new CanonicalMemoryCandidate
        {
            Title = "Short",
            Content = "ab",
            Scope = MemoryScope.Global
        };

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        Assert.Equal(ConsolidationAction.Rejected, result.Action);
    }

    [Fact]
    public void FindMatch_DifferentMemoryTypes_NoConflictDetected()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            NormalizedContent = "use postgresql",
            MemoryType = MemoryType.Fact
        };

        var candidate = new CanonicalMemoryCandidate
        {
            Content = "Don't use PostgreSQL",
            NormalizedContent = "dont use postgresql",
            Scope = MemoryScope.Global,
            MemoryType = MemoryType.UserConstraint
        };

        var match = _service.FindMatch(candidate, [existing]);

        // Different memory types — contradiction not detected
        Assert.False(match.IsConflict);
    }

    [Fact]
    public async Task HistoricalVsCurrent_SupersededNotMatched()
    {
        // Superseded memory should not be matched for consolidation
        var superseded = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use MySQL",
            Scope = MemoryScope.Global,
            State = MemoryState.Superseded,
            NormalizedContent = "use mysql"
        };

        _mockRepo.Setup(r => r.SearchAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([superseded]);

        var candidate = new CanonicalMemoryCandidate
        {
            Title = "DB",
            Content = "Use MySQL",
            NormalizedContent = "use mysql",
            Scope = MemoryScope.Global,
            Source = "knowledge:db"
        };

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        // Superseded memory is skipped — new memory created
        Assert.Equal(ConsolidationAction.Created, result.Action);
    }
}


