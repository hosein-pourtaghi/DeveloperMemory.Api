using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class DeterministicPromptAnalyzerTests
{
    private readonly DeterministicPromptAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_DebuggingRequest_DetectsDebuggingIntent()
    {
        var result = _analyzer.Analyze("Fix this EF Core migration error");

        Assert.Equal(IntentType.Debugging, result.Intent);
        Assert.Equal(TaskType.Debugging, result.TaskType);
    }

    [Fact]
    public void Analyze_ArchitectureRequest_DetectsArchitectureIntent()
    {
        var result = _analyzer.Analyze("Design the next phase of the system architecture");

        Assert.Equal(IntentType.Architecture, result.Intent);
        Assert.Equal(TaskType.Architecture, result.TaskType);
    }

    [Fact]
    public void Analyze_DocumentationRequest_DetectsDocumentationIntent()
    {
        var result = _analyzer.Analyze("Write documentation for the API endpoints");

        Assert.Equal(IntentType.Documentation, result.Intent);
        Assert.Equal(TaskType.Documentation, result.TaskType);
    }

    [Fact]
    public void Analyze_ResearchRequest_DetectsResearchIntent()
    {
        var result = _analyzer.Analyze("Research alternatives to PostgreSQL for this project");

        Assert.Equal(IntentType.Research, result.Intent);
        Assert.Equal(TaskType.Research, result.TaskType);
    }

    [Fact]
    public void Analyze_RefactoringRequest_DetectsRefactoringIntent()
    {
        var result = _analyzer.Analyze("Refactor this method to use async patterns");

        Assert.Equal(IntentType.Refactoring, result.Intent);
        Assert.Equal(TaskType.Refactoring, result.TaskType);
    }

    [Fact]
    public void Analyze_PlanningRequest_DetectsPlanningIntent()
    {
        var result = _analyzer.Analyze("Plan the implementation of the notification system");

        Assert.Equal(IntentType.Planning, result.Intent);
        Assert.Equal(TaskType.Planning, result.TaskType);
    }

    [Fact]
    public void Analyze_GeneralRequest_DetectsGeneralIntent()
    {
        var result = _analyzer.Analyze("Hello");

        Assert.Equal(IntentType.General, result.Intent);
        Assert.Equal(TaskType.General, result.TaskType);
    }

    [Fact]
    public void Analyze_PerformanceRequest_DetectsPerformanceTaskType()
    {
        var result = _analyzer.Analyze("This query is very slow, optimize the performance");

        Assert.Equal(TaskType.Performance, result.TaskType);
    }

    [Fact]
    public void Analyze_EmptyRequest_ReturnsGeneral()
    {
        var result = _analyzer.Analyze("");

        Assert.Equal(IntentType.General, result.Intent);
        Assert.Equal(TaskType.General, result.TaskType);
        Assert.Empty(result.Keywords);
    }

    [Fact]
    public void Analyze_NullRequest_ReturnsGeneral()
    {
        var result = _analyzer.Analyze(null!);

        Assert.Equal(IntentType.General, result.Intent);
        Assert.Equal(string.Empty, result.OriginalRequest);
    }

    [Fact]
    public void Analyze_ExtractsTechnicalContext()
    {
        var result = _analyzer.Analyze("Fix this EF Core migration error in the PostgreSQL database");

        Assert.Contains("EF Core", result.TechnicalContext);
        Assert.Contains("PostgreSQL", result.TechnicalContext);
    }

    [Fact]
    public void Analyze_ExtractsExplicitConstraints()
    {
        var result = _analyzer.Analyze("Use \"PostgreSQL\" and avoid \"Redis\" for caching");

        Assert.NotEmpty(result.ExplicitConstraints);
    }

    [Fact]
    public void Analyze_DetectsRequestedOutput()
    {
        var result = _analyzer.Analyze("Return the results as JSON");

        Assert.Equal("json", result.RequestedOutput);
    }

    [Fact]
    public void Analyze_WithPromptContext_ProducesConsistentResults()
    {
        var context = new Domain.Entities.PromptContext
        {
            OriginalQuery = "test",
            UserId = "user1"
        };

        var result1 = _analyzer.Analyze("Implement a service", context);
        var result2 = _analyzer.Analyze("Implement a service", context);

        Assert.Equal(result1.Intent, result2.Intent);
        Assert.Equal(result1.TaskType, result2.TaskType);
    }

    [Fact]
    public void Analyze_Deterministic_ForSameInput()
    {
        var input = "Fix the memory retrieval bug in the privacy filter";
        var result1 = _analyzer.Analyze(input);
        var result2 = _analyzer.Analyze(input);

        Assert.Equal(result1.Intent, result2.Intent);
        Assert.Equal(result1.TaskType, result2.TaskType);
        Assert.Equal(result1.Keywords.Count, result2.Keywords.Count);
    }

    [Fact]
    public void Analyze_UserGoal_ContainsIntentSummary()
    {
        var result = _analyzer.Analyze("Create a new memory controller endpoint");

        Assert.NotEmpty(result.UserGoal);
        Assert.Contains("Code implementation", result.UserGoal);
    }
}
