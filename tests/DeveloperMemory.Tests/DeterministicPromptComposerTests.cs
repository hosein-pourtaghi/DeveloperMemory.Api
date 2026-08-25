using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Xunit;

namespace DeveloperMemory.Tests;

public class DeterministicPromptComposerTests
{
    private readonly DeterministicPromptComposer _composer = new();

    [Fact]
    public void Compose_IncludesOriginalRequest()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "Implement the feature",
            Intent = IntentType.Coding,
            TaskType = TaskType.Coding
        };

        var result = _composer.Compose(analysis, [], [], "Implement the feature");

        Assert.Contains("Implement the feature", result.ComposedPrompt);
    }

    [Fact]
    public void Compose_IncludesIntentAnalysis()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "Fix the bug",
            Intent = IntentType.Debugging,
            TaskType = TaskType.Debugging
        };

        var result = _composer.Compose(analysis, [], [], "Fix the bug");

        Assert.Contains("Debugging", result.Instructions);
    }

    [Fact]
    public void Compose_IncludesConstraintSections()
    {
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

        var result = _composer.Compose(analysis, constraints, [], "test");

        Assert.Contains("PostgreSQL", result.Instructions);
    }

    [Fact]
    public void Compose_IncludesContextSections()
    {
        var analysis = new PromptAnalysis { OriginalRequest = "test" };
        var sections = new List<ContextSection>
        {
            new ContextSection
            {
                SectionId = "project_context",
                Heading = "Project Context",
                Order = 10,
                Items =
                [
                    new ContextItem { Content = "Project uses Clean Architecture", Importance = 0.8 }
                ]
            }
        };

        var result = _composer.Compose(analysis, [], sections, "test");

        Assert.Contains("Project Context", result.Instructions);
        Assert.Contains("Clean Architecture", result.Instructions);
    }

    [Fact]
    public void Compose_PreservesOriginalRequestExactly()
    {
        var analysis = new PromptAnalysis { OriginalRequest = "test" };
        var originalRequest = "Please implement the authentication module with JWT tokens";

        var result = _composer.Compose(analysis, [], [], originalRequest);

        Assert.Contains($"User Request: {originalRequest}", result.ComposedPrompt);
    }

    [Fact]
    public void Compose_EmptySections_ProducesValidPrompt()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "Hello",
            Intent = IntentType.General
        };

        var result = _composer.Compose(analysis, [], [], "Hello");

        Assert.NotEmpty(result.Instructions);
        Assert.NotEmpty(result.ComposedPrompt);
        Assert.Contains("Hello", result.ComposedPrompt);
    }

    [Fact]
    public void Compose_Deterministic_ForSameInput()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "test",
            Intent = IntentType.Coding,
            TaskType = TaskType.Coding
        };

        var result1 = _composer.Compose(analysis, [], [], "test");
        var result2 = _composer.Compose(analysis, [], [], "test");

        Assert.Equal(result1.Instructions, result2.Instructions);
        Assert.Equal(result1.ComposedPrompt, result2.ComposedPrompt);
    }

    [Fact]
    public void Compose_IncludesIntelligenceContextHeader()
    {
        var analysis = new PromptAnalysis { OriginalRequest = "test" };

        var result = _composer.Compose(analysis, [], [], "test");

        Assert.Contains("--- DeveloperMemory Intelligence Context ---", result.Instructions);
        Assert.Contains("--- End Intelligence Context ---", result.Instructions);
    }

    [Fact]
    public void Compose_IncludesTechnicalContext()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "test",
            TechnicalContext = ["EF Core", "PostgreSQL"]
        };

        var result = _composer.Compose(analysis, [], [], "test");

        Assert.Contains("EF Core", result.Instructions);
        Assert.Contains("PostgreSQL", result.Instructions);
    }
}
