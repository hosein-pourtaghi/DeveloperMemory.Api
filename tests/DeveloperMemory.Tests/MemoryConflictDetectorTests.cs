using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Xunit;

namespace DeveloperMemory.Tests;

public class MemoryConflictDetectorTests
{
    private readonly MemoryConflictDetector _detector = new();

    [Fact]
    public void DetectConflicts_ExactDuplicate_ReturnsExactDuplicate()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL for the database",
            Scope = MemoryScope.Project,
            State = MemoryState.Active,
            MemoryType = MemoryType.TechnicalDecision
        };

        var newMemory = new MemoryEntry
        {
            Content = "Use PostgreSQL for the database",
            Scope = MemoryScope.Project,
            MemoryType = MemoryType.TechnicalDecision
        };

        var conflicts = _detector.DetectConflicts(newMemory, [existing]);

        Assert.Single(conflicts);
        Assert.Equal(MemoryConflictType.ExactDuplicate, conflicts[0].ConflictType);
        Assert.Equal(1.0, conflicts[0].Confidence);
    }

    [Fact]
    public void DetectConflicts_NormalizedDuplicate_ReturnsNormalizedDuplicate()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Uses PostgreSQL",
            NormalizedContent = "uses postgresql",
            Scope = MemoryScope.Project,
            State = MemoryState.Active
        };

        var newMemory = new MemoryEntry
        {
            Content = "uses postgresql.",
            NormalizedContent = "uses postgresql",
            Scope = MemoryScope.Project
        };

        var conflicts = _detector.DetectConflicts(newMemory, [existing]);

        Assert.Single(conflicts);
        Assert.Equal(MemoryConflictType.NormalizedDuplicate, conflicts[0].ConflictType);
    }

    [Fact]
    public void DetectConflicts_NoConflict_ReturnsEmpty()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Uses PostgreSQL",
            Scope = MemoryScope.Project,
            State = MemoryState.Active
        };

        var newMemory = new MemoryEntry
        {
            Content = "Prefers React for the frontend",
            Scope = MemoryScope.Project
        };

        var conflicts = _detector.DetectConflicts(newMemory, [existing]);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void DetectConflicts_SkipsDeletedMemories()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Project,
            State = MemoryState.Deleted
        };

        var newMemory = new MemoryEntry
        {
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Project
        };

        var conflicts = _detector.DetectConflicts(newMemory, [existing]);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void DetectConflicts_DifferentScopes_NoConflict()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Project,
            State = MemoryState.Active
        };

        var newMemory = new MemoryEntry
        {
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Global
        };

        var conflicts = _detector.DetectConflicts(newMemory, [existing]);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void DetectConflicts_HighSimilarity_ReturnsUpdatedVersion()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "The project uses PostgreSQL as the primary database with Entity Framework Core for ORM and migrations",
            Scope = MemoryScope.Project,
            State = MemoryState.Active
        };

        var newMemory = new MemoryEntry
        {
            Content = "The project uses PostgreSQL as the primary database with Entity Framework Core for ORM and data access",
            Scope = MemoryScope.Project
        };

        var conflicts = _detector.DetectConflicts(newMemory, [existing]);

        Assert.NotEmpty(conflicts);
        Assert.Contains(conflicts, c => c.ConflictType == MemoryConflictType.UpdatedVersion);
    }

    [Fact]
    public void DetectConflicts_EmptyExisting_ReturnsEmpty()
    {
        var newMemory = new MemoryEntry
        {
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Project
        };

        var conflicts = _detector.DetectConflicts(newMemory, []);

        Assert.Empty(conflicts);
    }
}
