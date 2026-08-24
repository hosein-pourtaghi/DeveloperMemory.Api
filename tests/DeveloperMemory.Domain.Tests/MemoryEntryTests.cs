using Xunit;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Tests;

public class MemoryEntryTests
{
    [Fact]
    public void NewMemoryEntry_HasDefaultValues()
    {
        var entry = new MemoryEntry();

        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(string.Empty, entry.Title);
        Assert.Equal(string.Empty, entry.Content);
        Assert.Equal(MemoryScope.Global, entry.Scope);
        Assert.Equal(MemoryState.Active, entry.State);
        Assert.Equal(DataClassification.Internal, entry.Classification);
        Assert.Null(entry.ProjectId);
        Assert.Null(entry.ExpiresAt);
        Assert.Equal(0.5, entry.Importance);
        Assert.True(entry.IsActive);
        Assert.False(entry.IsExpired);
    }

    [Fact]
    public void SetTags_SerializesToTagsJson()
    {
        var entry = new MemoryEntry();
        var tags = new List<string> { "dotnet", "postgresql", "docker" };

        entry.SetTags(tags);

        Assert.NotNull(entry.TagsJson);
        Assert.Equal(tags, entry.Tags);
    }

    [Fact]
    public void Tags_ReturnsEmptyList_WhenTagsJsonIsNull()
    {
        var entry = new MemoryEntry();

        Assert.Empty(entry.Tags);
    }

    [Fact]
    public void Supersede_SetsStateAndLink()
    {
        var entry = new MemoryEntry { State = MemoryState.Active };
        var replacementId = Guid.NewGuid();

        entry.Supersede(replacementId);

        Assert.Equal(MemoryState.Superseded, entry.State);
        Assert.Equal(replacementId, entry.SupersededById);
        Assert.False(entry.IsActive);
    }

    [Fact]
    public void Expire_SetsStateToExpired()
    {
        var entry = new MemoryEntry { State = MemoryState.Active };

        entry.Expire();

        Assert.Equal(MemoryState.Expired, entry.State);
        Assert.False(entry.IsActive);
    }

    [Fact]
    public void Archive_SetsStateToArchived()
    {
        var entry = new MemoryEntry { State = MemoryState.Active };

        entry.Archive();

        Assert.Equal(MemoryState.Archived, entry.State);
        Assert.False(entry.IsActive);
    }

    [Fact]
    public void SoftDelete_SetsStateToDeleted()
    {
        var entry = new MemoryEntry { State = MemoryState.Active };

        entry.SoftDelete();

        Assert.Equal(MemoryState.Deleted, entry.State);
        Assert.False(entry.IsActive);
    }

    [Fact]
    public void IsExpired_ReturnsTrue_WhenExpiresAtIsInPast()
    {
        var entry = new MemoryEntry
        {
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };

        Assert.True(entry.IsExpired);
    }

    [Fact]
    public void IsExpired_ReturnsFalse_WhenExpiresAtIsInFuture()
    {
        var entry = new MemoryEntry
        {
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        Assert.False(entry.IsExpired);
    }

    [Fact]
    public void IsExpired_ReturnsFalse_WhenExpiresAtIsNull()
    {
        var entry = new MemoryEntry();

        Assert.False(entry.IsExpired);
    }

    [Theory]
    [InlineData(MemoryScope.Global)]
    [InlineData(MemoryScope.Project)]
    [InlineData(MemoryScope.Workspace)]
    [InlineData(MemoryScope.Private)]
    public void MemoryEntry_SupportsAllScopes(MemoryScope scope)
    {
        var entry = new MemoryEntry { Scope = scope };
        Assert.Equal(scope, entry.Scope);
    }

    [Theory]
    [InlineData(MemoryState.Active)]
    [InlineData(MemoryState.Updated)]
    [InlineData(MemoryState.Superseded)]
    [InlineData(MemoryState.Expired)]
    [InlineData(MemoryState.Archived)]
    [InlineData(MemoryState.Deleted)]
    public void MemoryEntry_SupportsAllStates(MemoryState state)
    {
        var entry = new MemoryEntry { State = state };
        Assert.Equal(state, entry.State);
    }
}
