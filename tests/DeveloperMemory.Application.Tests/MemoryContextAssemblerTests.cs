using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class MemoryContextAssemblerTests
{
    private readonly MemoryContextAssembler _assembler = new();

    [Fact]
    public void Assemble_EmptyContext_ReturnsEmptySections()
    {
        var context = new PromptContext();
        var analysis = new PromptAnalysis { OriginalRequest = "test" };

        var result = _assembler.Assemble(context, analysis, []);

        Assert.Empty(result.Sections);
        Assert.Equal(0, result.DuplicatesRemoved);
    }

    [Fact]
    public void Assemble_GroupsProjectMemoriesIntoProjectSection()
    {
        var projectId = Guid.NewGuid();
        var context = new PromptContext
        {
            RetrievedMemories =
            [
                new RetrievedMemory
                {
                    MemoryId = Guid.NewGuid(),
                    Title = "Project Config",
                    Content = "Use PostgreSQL",
                    Scope = MemoryScope.Project,
                    ProjectId = projectId,
                    Importance = 0.8
                }
            ]
        };
        var analysis = new PromptAnalysis { OriginalRequest = "test" };

        var result = _assembler.Assemble(context, analysis, []);

        Assert.Contains(result.Sections, s => s.SectionId == "project_context");
    }

    [Fact]
    public void Assemble_GroupsGlobalMemoriesIntoRelevantSection()
    {
        var context = new PromptContext
        {
            RetrievedMemories =
            [
                new RetrievedMemory
                {
                    MemoryId = Guid.NewGuid(),
                    Title = "Global Rule",
                    Content = "Always write tests",
                    Scope = MemoryScope.Global,
                    Importance = 0.7
                }
            ]
        };
        var analysis = new PromptAnalysis { OriginalRequest = "test" };

        var result = _assembler.Assemble(context, analysis, []);

        Assert.Contains(result.Sections, s => s.SectionId == "relevant_memory");
    }

    [Fact]
    public void Assemble_GroupsWorkspaceMemoriesIntoWorkspaceSection()
    {
        var context = new PromptContext
        {
            RetrievedMemories =
            [
                new RetrievedMemory
                {
                    MemoryId = Guid.NewGuid(),
                    Title = "Workspace Setting",
                    Content = "Dev environment config",
                    Scope = MemoryScope.Workspace,
                    Importance = 0.6
                }
            ]
        };
        var analysis = new PromptAnalysis { OriginalRequest = "test" };

        var result = _assembler.Assemble(context, analysis, []);

        Assert.Contains(result.Sections, s => s.SectionId == "workspace_context");
    }

    [Fact]
    public void Assemble_DeduplicatesSimilarMemories()
    {
        var context = new PromptContext
        {
            RetrievedMemories =
            [
                new RetrievedMemory
                {
                    MemoryId = Guid.NewGuid(),
                    Title = "DB Choice",
                    Content = "Project uses PostgreSQL as the primary database",
                    Scope = MemoryScope.Project,
                    Importance = 0.8,
                    UpdatedAt = DateTime.UtcNow
                },
                new RetrievedMemory
                {
                    MemoryId = Guid.NewGuid(),
                    Title = "Database",
                    Content = "Project uses PostgreSQL as the primary database",
                    Scope = MemoryScope.Project,
                    Importance = 0.7,
                    UpdatedAt = DateTime.UtcNow.AddDays(-1)
                }
            ]
        };
        var analysis = new PromptAnalysis { OriginalRequest = "test" };

        var result = _assembler.Assemble(context, analysis, []);

        Assert.True(result.DuplicatesRemoved > 0);
    }
    [Fact]
    public void Assemble_IncludesConstraintSections()
    {
        var context = new PromptContext
        {
            RetrievedMemories =
            [
                new RetrievedMemory
                {
                    MemoryId = Guid.NewGuid(),
                    Title = "Test Memory",
                    Content = "Some content",
                    Scope = MemoryScope.Global,
                    Importance = 0.5
                }
            ]
        };
        var analysis = new PromptAnalysis { OriginalRequest = "test" };
        var constraints = new List<PromptConstraint>
        {
            new PromptConstraint
            {
                Type = ConstraintType.Technology,
                Value = "Use PostgreSQL",
                Source = ConstraintSource.ProjectRule,
                Precedence = 80
            }
        };

        var result = _assembler.Assemble(context, analysis, constraints);

        Assert.Contains(result.Sections, s => s.SectionId == "constraints");
    }

    [Fact]
    public void Assemble_SectionsOrderedByPriority()
    {
        var context = new PromptContext
        {
            RetrievedMemories =
            [
                new RetrievedMemory
                {
                    MemoryId = Guid.NewGuid(),
                    Title = "Project Memory",
                    Content = "Project config",
                    Scope = MemoryScope.Project,
                    Importance = 0.8
                },
                new RetrievedMemory
                {
                    MemoryId = Guid.NewGuid(),
                    Title = "Global Memory",
                    Content = "Global rule",
                    Scope = MemoryScope.Global,
                    Importance = 0.7
                },
                new RetrievedMemory
                {
                    MemoryId = Guid.NewGuid(),
                    Title = "Workspace Memory",
                    Content = "Workspace config",
                    Scope = MemoryScope.Workspace,
                    Importance = 0.6
                }
            ]
        };
        var analysis = new PromptAnalysis { OriginalRequest = "test" };

        var result = _assembler.Assemble(context, analysis, []);

        var sectionOrders = result.Sections.Select(s => s.Order).ToList();
        Assert.Equal(sectionOrders, sectionOrders.OrderBy(x => x).ToList());
    }

    [Fact]
    public void Assemble_NoDuplicates_ReturnsZeroDuplicatesRemoved()
    {
        var context = new PromptContext
        {
            RetrievedMemories =
            [
                new RetrievedMemory
                {
                    MemoryId = Guid.NewGuid(),
                    Title = "Memory A",
                    Content = "Completely different content about topic A",
                    Scope = MemoryScope.Global,
                    Importance = 0.5
                },
                new RetrievedMemory
                {
                    MemoryId = Guid.NewGuid(),
                    Title = "Memory B",
                    Content = "Entirely different content about topic B",
                    Scope = MemoryScope.Project,
                    Importance = 0.6
                }
            ]
        };
        var analysis = new PromptAnalysis { OriginalRequest = "test" };

        var result = _assembler.Assemble(context, analysis, []);

        Assert.Equal(0, result.DuplicatesRemoved);
    }

    [Fact]
    public void Assemble_ItemsOrderedByImportanceWithinSection()
    {
        var context = new PromptContext
        {
            RetrievedMemories =
            [
                new RetrievedMemory
                {
                    MemoryId = Guid.NewGuid(),
                    Title = "Low Importance",
                    Content = "Low",
                    Scope = MemoryScope.Global,
                    Importance = 0.2
                },
                new RetrievedMemory
                {
                    MemoryId = Guid.NewGuid(),
                    Title = "High Importance",
                    Content = "High",
                    Scope = MemoryScope.Global,
                    Importance = 0.9
                }
            ]
        };
        var analysis = new PromptAnalysis { OriginalRequest = "test" };

        var result = _assembler.Assemble(context, analysis, []);

        var globalSection = result.Sections.First(s => s.SectionId == "relevant_memory");
        Assert.True(globalSection.Items[0].Importance >= globalSection.Items[1].Importance);
    }
}
