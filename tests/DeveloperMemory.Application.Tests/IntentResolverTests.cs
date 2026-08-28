using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Configuration;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class IntentResolverTests
{
    private readonly Mock<ILogger<IntentResolver>> _loggerMock = new();
    private readonly IntentResolver _resolver;

    public IntentResolverTests()
    {
        _resolver = new IntentResolver(_loggerMock.Object);
    }

    [Fact]
    public void Resolve_DeterministicOnly_UsesDeterministic()
    {
        var deterministic = new IntentAnalysisResult
        {
            Intent = IntentType.Coding,
            TaskType = TaskType.Coding,
            Keywords = ["implement", "service"]
        };

        var result = _resolver.Resolve(deterministic);

        Assert.Equal(IntentType.Coding, result.Intent);
    }

    [Fact]
    public void Resolve_Agreement_UsesDeterministic()
    {
        var deterministic = new IntentAnalysisResult
        {
            Intent = IntentType.Debugging,
            TaskType = TaskType.Debugging,
            Keywords = ["fix", "error"]
        };

        var llm = new IntentAnalysisResult
        {
            Intent = IntentType.Debugging,
            TaskType = TaskType.Debugging
        };

        var result = _resolver.Resolve(deterministic, llm);

        Assert.Equal(IntentType.Debugging, result.Intent);
    }

    [Fact]
    public void Resolve_HighConfidenceDeterministic_PreferDeterministic()
    {
        var deterministic = new IntentAnalysisResult
        {
            Intent = IntentType.Architecture,
            TaskType = TaskType.Architecture,
            Keywords = ["design", "architecture"],
            TechnicalContext = ["Clean Architecture"]
        };

        var llm = new IntentAnalysisResult
        {
            Intent = IntentType.Coding,
            TaskType = TaskType.Coding
        };

        var result = _resolver.Resolve(deterministic, llm);

        Assert.Equal(IntentType.Architecture, result.Intent);
    }
}
