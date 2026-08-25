using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace DeveloperMemory.Tests;

/// <summary>
/// Tests for lifecycle filtering.
/// Proves that only active memories are returned and expired/superseded/deleted/archived are excluded.
/// </summary>
public class LifecycleFilterTests
{
    [Fact]
    public void ActiveMemory_IsReturned()
    {
        var activeMemory = TestDataHelper.CreateMemory(state: MemoryState.Active);
        var input = new List<(MemoryEntry Memory, string EligibilityReason)>
        {
            (activeMemory, "Global scope")
        };

        var results = LifecycleFilter.FilterByLifecycle(input);

        results.Should().HaveCount(1);
        results[0].Memory.State.Should().Be(MemoryState.Active);
    }

    [Fact]
    public void UpdatedMemory_IsReturned()
    {
        var updatedMemory = TestDataHelper.CreateMemory(state: MemoryState.Updated);
        var input = new List<(MemoryEntry Memory, string EligibilityReason)>
        {
            (updatedMemory, "Global scope")
        };

        var results = LifecycleFilter.FilterByLifecycle(input);

        results.Should().HaveCount(1);
    }

    [Fact]
    public void SupersededMemory_IsExcluded()
    {
        var supersededMemory = TestDataHelper.CreateMemory(state: MemoryState.Superseded);
        var input = new List<(MemoryEntry Memory, string EligibilityReason)>
        {
            (supersededMemory, "Global scope")
        };

        var results = LifecycleFilter.FilterByLifecycle(input);

        results.Should().BeEmpty("Superseded memories should not be returned in active context");
    }

    [Fact]
    public void ExpiredMemory_IsExcluded()
    {
        var expiredMemory = TestDataHelper.CreateMemory(
            state: MemoryState.Active, expiresAt: DateTime.UtcNow.AddDays(-1));

        var input = new List<(MemoryEntry Memory, string EligibilityReason)>
        {
            (expiredMemory, "Global scope")
        };

        var results = LifecycleFilter.FilterByLifecycle(input);

        results.Should().BeEmpty("Expired memories should not be returned");
    }

    [Fact]
    public void ArchivedMemory_IsExcluded()
    {
        var archivedMemory = TestDataHelper.CreateMemory(state: MemoryState.Archived);
        var input = new List<(MemoryEntry Memory, string EligibilityReason)>
        {
            (archivedMemory, "Global scope")
        };

        var results = LifecycleFilter.FilterByLifecycle(input);

        results.Should().BeEmpty("Archived memories should not be returned in active context");
    }

    [Fact]
    public void DeletedMemory_IsExcluded()
    {
        var deletedMemory = TestDataHelper.CreateMemory(state: MemoryState.Deleted);
        var input = new List<(MemoryEntry Memory, string EligibilityReason)>
        {
            (deletedMemory, "Global scope")
        };

        var results = LifecycleFilter.FilterByLifecycle(input);

        results.Should().BeEmpty("Deleted memories must never appear in normal retrieval");
    }

    [Fact]
    public void NonExpiredActiveMemory_IsReturned()
    {
        var activeMemory = TestDataHelper.CreateMemory(
            state: MemoryState.Active, expiresAt: DateTime.UtcNow.AddDays(30));

        var input = new List<(MemoryEntry Memory, string EligibilityReason)>
        {
            (activeMemory, "Global scope")
        };

        var results = LifecycleFilter.FilterByLifecycle(input);

        results.Should().HaveCount(1);
    }

    [Fact]
    public void MixedLifecycle_MemoryEntries_OnlyActiveReturned()
    {
        var active = TestDataHelper.CreateMemory(title: "Active", state: MemoryState.Active);
        var superseded = TestDataHelper.CreateMemory(title: "Superseded", state: MemoryState.Superseded);
        var expired = TestDataHelper.CreateMemory(
            title: "Expired", state: MemoryState.Active, expiresAt: DateTime.UtcNow.AddDays(-1));
        var archived = TestDataHelper.CreateMemory(title: "Archived", state: MemoryState.Archived);
        var deleted = TestDataHelper.CreateMemory(title: "Deleted", state: MemoryState.Deleted);

        var input = new List<(MemoryEntry Memory, string EligibilityReason)>
        {
            (active, "Global scope"),
            (superseded, "Global scope"),
            (expired, "Global scope"),
            (archived, "Global scope"),
            (deleted, "Global scope")
        };

        var results = LifecycleFilter.FilterByLifecycle(input);

        results.Should().HaveCount(1);
        results[0].Memory.Title.Should().Be("Active");
    }

    [Fact]
    public void SupersededAndReplacement_OnlyReplacementReturned()
    {
        // Arrange: Memory A superseded by Memory B
        var memoryA = TestDataHelper.CreateMemory(
            title: "Original", state: MemoryState.Superseded, importance: 0.8);
        var memoryB = TestDataHelper.CreateMemory(
            title: "Replacement", state: MemoryState.Active, importance: 0.9);

        var input = new List<(MemoryEntry Memory, string EligibilityReason)>
        {
            (memoryA, "Global scope"),
            (memoryB, "Global scope")
        };

        // Act
        var results = LifecycleFilter.FilterByLifecycle(input);

        // Assert: only the replacement should be returned
        results.Should().HaveCount(1);
        results[0].Memory.Title.Should().Be("Replacement");
    }

    [Fact]
    public void IsEligible_ReturnsTrueForActiveMemory()
    {
        var memory = TestDataHelper.CreateMemory(state: MemoryState.Active);
        LifecycleFilter.IsEligible(memory).Should().BeTrue();
    }

    [Fact]
    public void IsEligible_ReturnsFalseForSupersededMemory()
    {
        var memory = TestDataHelper.CreateMemory(state: MemoryState.Superseded);
        LifecycleFilter.IsEligible(memory).Should().BeFalse();
    }

    [Fact]
    public void IsEligible_ReturnsFalseForExpiredMemory()
    {
        var memory = TestDataHelper.CreateMemory(
            state: MemoryState.Active, expiresAt: DateTime.UtcNow.AddDays(-1));
        LifecycleFilter.IsEligible(memory).Should().BeFalse();
    }
}
