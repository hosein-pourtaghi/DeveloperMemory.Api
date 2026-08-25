using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Tests;

/// <summary>
/// Helper for creating test data in a consistent manner.
/// </summary>
public static class TestDataHelper
{
    public static MemoryEntry CreateMemory(
        string title = "Test Memory",
        string content = "Test content",
        MemoryScope scope = MemoryScope.Global,
        MemoryState state = MemoryState.Active,
        Guid? projectId = null,
        string? workspaceId = null,
        string? userId = null,
        double importance = 0.5,
        List<string>? tags = null,
        DateTime? updatedAt = null,
        DateTime? expiresAt = null)
    {
        var entry = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Title = title,
            Content = content,
            Scope = scope,
            State = state,
            ProjectId = projectId,
            WorkspaceId = workspaceId,
            UserId = userId,
            Importance = importance,
            Source = "test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = updatedAt ?? DateTime.UtcNow,
            ExpiresAt = expiresAt
        };

        if (tags != null)
        {
            entry.SetTags(tags);
        }

        return entry;
    }

    public static Project CreateProject(string name = "Test Project")
    {
        return new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"Description for {name}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static RetrievalRequest CreateRetrievalRequest(
        string query = "test",
        Guid? projectId = null,
        string? workspaceId = null,
        string userId = "test-user",
        int maxResults = 20,
        int tokenBudget = 2000)
    {
        return new RetrievalRequest
        {
            Query = query,
            ProjectId = projectId,
            WorkspaceId = workspaceId,
            UserId = userId,
            MaximumResults = maxResults,
            ContextTokenBudget = tokenBudget
        };
    }
}
