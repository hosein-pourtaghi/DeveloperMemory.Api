using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Xunit;

namespace DeveloperMemory.Domain.Tests;

public class MemoryLifecycleTests
{
    [Fact]
    public void ValidTransition_Active_To_Updated()
    {
        Assert.True(MemoryEntry.IsValidTransition(MemoryState.Active, MemoryState.Updated));
    }

    [Fact]
    public void ValidTransition_Active_To_Superseded()
    {
        Assert.True(MemoryEntry.IsValidTransition(MemoryState.Active, MemoryState.Superseded));
    }

    [Fact]
    public void ValidTransition_Active_To_Expired()
    {
        Assert.True(MemoryEntry.IsValidTransition(MemoryState.Active, MemoryState.Expired));
    }

    [Fact]
    public void ValidTransition_Active_To_Archived()
    {
        Assert.True(MemoryEntry.IsValidTransition(MemoryState.Active, MemoryState.Archived));
    }

    [Fact]
    public void ValidTransition_Active_To_Deleted()
    {
        Assert.True(MemoryEntry.IsValidTransition(MemoryState.Active, MemoryState.Deleted));
    }

    [Fact]
    public void InvalidTransition_Superseded_IsTerminal()
    {
        Assert.False(MemoryEntry.IsValidTransition(MemoryState.Superseded, MemoryState.Active));
    }

    [Fact]
    public void InvalidTransition_Expired_IsTerminal()
    {
        Assert.False(MemoryEntry.IsValidTransition(MemoryState.Expired, MemoryState.Active));
    }

    [Fact]
    public void InvalidTransition_Deleted_IsTerminal()
    {
        Assert.False(MemoryEntry.IsValidTransition(MemoryState.Deleted, MemoryState.Active));
    }

    [Fact]
    public void Supersede_IncrementsVersion()
    {
        var memory = new MemoryEntry { State = MemoryState.Active, Version = 1 };
        memory.Supersede(Guid.NewGuid());
        Assert.Equal(2, memory.Version);
    }

    [Fact]
    public void Expire_IncrementsVersion()
    {
        var memory = new MemoryEntry { State = MemoryState.Active, Version = 1 };
        memory.Expire();
        Assert.Equal(2, memory.Version);
    }

    [Fact]
    public void Archive_IncrementsVersion()
    {
        var memory = new MemoryEntry { State = MemoryState.Active, Version = 1 };
        memory.Archive();
        Assert.Equal(2, memory.Version);
    }

    [Fact]
    public void SoftDelete_IncrementsVersion()
    {
        var memory = new MemoryEntry { State = MemoryState.Active, Version = 1 };
        memory.SoftDelete();
        Assert.Equal(2, memory.Version);
    }

    [Fact]
    public void Supersede_Throws_WhenStateIsSuperseded()
    {
        var memory = new MemoryEntry { State = MemoryState.Superseded };
        Assert.Throws<InvalidOperationException>(() => memory.Supersede(Guid.NewGuid()));
    }

    [Fact]
    public void Expire_Throws_WhenStateIsDeleted()
    {
        var memory = new MemoryEntry { State = MemoryState.Deleted };
        Assert.Throws<InvalidOperationException>(() => memory.Expire());
    }

    [Fact]
    public void MarkAccessed_IncrementsCount()
    {
        var memory = new MemoryEntry { AccessCount = 0 };
        memory.MarkAccessed();
        Assert.Equal(1, memory.AccessCount);
        Assert.NotNull(memory.LastAccessedAt);
    }

    [Fact]
    public void IsExpired_True_WhenExpiresAtInPast()
    {
        var memory = new MemoryEntry { ExpiresAt = DateTime.UtcNow.AddDays(-1) };
        Assert.True(memory.IsExpired);
    }

    [Fact]
    public void IsExpired_False_WhenExpiresAtInFuture()
    {
        var memory = new MemoryEntry { ExpiresAt = DateTime.UtcNow.AddDays(1) };
        Assert.False(memory.IsExpired);
    }

    [Fact]
    public void ComputeNormalizedContent_LowercasesAndStripsPunctuation()
    {
        var memory = new MemoryEntry
        {
            Title = "Test!",
            Content = "Hello, World. This is a test."
        };

        var normalized = memory.ComputeNormalizedContent();
        Assert.Equal("test hello world this is a test", normalized);
    }
}
