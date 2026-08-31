using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Domain.Enums;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class DeterministicIntentAnalyzerTests
{
    private readonly DeterministicIntentAnalyzer _analyzer = new();

    [Fact]
    public async Task AnalyzeAsync_EmptyInput_ReturnsGeneral()
    {
        var result = await _analyzer.AnalyzeAsync("");

        Assert.Equal(IntentType.General, result.Intent);
        Assert.True(result.IsSimpleQuery);
    }

    [Fact]
    public async Task AnalyzeAsync_DebuggingRequest_DetectsDebuggingIntent()
    {
        var result = await _analyzer.AnalyzeAsync("Fix this EF Core migration error");

        Assert.Equal(IntentType.Debugging, result.Intent);
        Assert.Equal(TaskType.Debugging, result.TaskType);
        Assert.Equal("Database", result.TechnicalDomain);
    }

    [Fact]
    public async Task AnalyzeAsync_ArchitectureRequest_DetectsArchitectureIntent()
    {
        var result = await _analyzer.AnalyzeAsync("Design the next phase of the architecture");

        Assert.Equal(IntentType.Architecture, result.Intent);
        Assert.True(result.RequiresProjectContext);
    }

    [Fact]
    public async Task AnalyzeAsync_MemoryInstruction_DetectsInstruction()
    {
        var result = await _analyzer.AnalyzeAsync("Remember that we always use PostgreSQL");

        Assert.True(result.IsMemoryInstruction);
        Assert.Contains("PostgreSQL", result.TechnicalContext);
    }

    [Fact]
    public async Task AnalyzeAsync_PerformanceRequest_DetectsPerformanceDomain()
    {
        var result = await _analyzer.AnalyzeAsync("This query is very slow, optimize performance");

        Assert.Equal(TaskType.Performance, result.TaskType);
    }

    [Fact]
    public async Task AnalyzeAsync_SimpleQuery_IsSimpleQuery()
    {
        var result = await _analyzer.AnalyzeAsync("Hello");

        Assert.True(result.IsSimpleQuery);
        Assert.Equal(ComplexityLevel.Simple, result.Complexity);
    }

    [Fact]
    public async Task AnalyzeAsync_ComplexRequest_HigherComplexity()
    {
        var result = await _analyzer.AnalyzeAsync(
            "Implement a complex distributed microservice architecture with multiple services communicating through event-driven patterns");

        Assert.True(result.Complexity >= ComplexityLevel.Complex);
    }

    [Fact]
    public async Task AnalyzeAsync_DangerousRequest_HighRisk()
    {
        var result = await _analyzer.AnalyzeAsync("Delete the production database migration");

        Assert.Equal(RiskLevel.High, result.RiskLevel);
    }

    [Fact]
    public async Task AnalyzeAsync_WithProjectContext_RequiresProjectContext()
    {
        var result = await _analyzer.AnalyzeAsync("What architecture does this project use?");

        Assert.True(result.RequiresProjectContext);
        Assert.Contains(RequiredContextType.ProjectArchitecture, result.RequiredContext);
    }
}
