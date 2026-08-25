using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Tests;

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

    [Fact]
    public void Resolve_GeneralDeterministicHighLlm_MergesSignals()
    {
        var deterministic = new IntentAnalysisResult
        {
            Intent = IntentType.General,
            TaskType = TaskType.General,
            Keywords = []
        };

        var llm = new IntentAnalysisResult
        {
            Intent = IntentType.Debugging,
            TaskType = TaskType.Debugging,
            Keywords = ["fix", "error", "bug"]
        };

        var result = _resolver.Resolve(deterministic, llm);

        // Should use LLM when deterministic is general
        Assert.Equal(IntentType.Debugging, result.Intent);
    }

    [Fact]
    public void Resolve_MergesKeywords()
    {
        var deterministic = new IntentAnalysisResult
        {
            Intent = IntentType.Coding,
            Keywords = ["implement", "service"]
        };

        var llm = new IntentAnalysisResult
        {
            Intent = IntentType.Coding,
            Keywords = ["authentication", "jwt"]
        };

        var result = _resolver.Resolve(deterministic, llm);

        Assert.Contains("implement", result.Keywords);
        Assert.Contains("jwt", result.Keywords);
    }
}

public class PromptValidatorTests
{
    private readonly Mock<ILogger<PromptValidator>> _loggerMock = new();
    private readonly PromptValidator _validator;

    public PromptValidatorTests()
    {
        _validator = new PromptValidator(_loggerMock.Object);
    }

    [Fact]
    public void Validate_EmptyOutput_Invalid()
    {
        var result = _validator.Validate("Original prompt", "");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Contains("empty"));
    }

    [Fact]
    public void Validate_TokenBudgetExceeded_Flagged()
    {
        var original = "--- USER REQUEST ---\nTest request\n---";
        var optimized = new string('x', 20000); // Very long

        var result = _validator.Validate(original, optimized, tokenBudget: 100);

        Assert.True(result.BudgetExceeded);
    }

    [Fact]
    public void Validate_CriticalConstraintMissing_Flagged()
    {
        var original = "Use PostgreSQL for the database";
        var optimized = "Use MySQL for the database";

        var intent = new IntentAnalysisResult
        {
            ExplicitConstraints = ["PostgreSQL"]
        };

        var result = _validator.Validate(original, optimized, intent);

        Assert.False(result.IsValid);
        Assert.True(result.CriticalConstraintMissing);
    }

    [Fact]
    public void Validate_SecurityBoundaryMissing_Flagged()
    {
        var original = "--- RETRIEVED CONTEXT ---\nMemory content\n---";
        var optimized = "Just the optimized prompt without boundaries";

        var result = _validator.Validate(original, optimized);

        Assert.True(result.SecurityBoundaryMissing);
    }

    [Fact]
    public void Validate_TooDifferent_Invalid()
    {
        var original = "Use PostgreSQL for the database with connection pooling";
        var optimized = "Completely unrelated content about cooking recipes";

        var result = _validator.Validate(original, optimized);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_ValidOptimization_Passes()
    {
        var original = "--- RETRIEVED CONTEXT ---\nMemory content\n--- USER REQUEST ---\nImplement feature\n---";
        var optimized = "--- RETRIEVED CONTEXT ---\nMemory content\n--- USER REQUEST ---\nImplement the new feature\n---";

        var result = _validator.Validate(original, optimized);

        Assert.True(result.IsValid);
    }
}

public class PromptProfileTests
{
    [Fact]
    public void PromptProfile_GetConfiguration_ReturnsParsedConfig()
    {
        var profile = new PromptProfile
        {
            ConfigurationJson = "{\"tokenBudget\":8000}"
        };

        var config = profile.GetConfiguration();

        Assert.Equal(8000, config.TokenBudget);
    }

    [Fact]
    public void PromptProfile_SetConfiguration_UpdatesJson()
    {
        var profile = new PromptProfile();
        var config = new PromptProfileConfiguration { TokenBudget = 6000 };

        profile.SetConfiguration(config);

        var parsed = profile.GetConfiguration();
        Assert.Equal(6000, parsed.TokenBudget);
    }

    [Fact]
    public void PromptProfileProvider_DefaultProfiles Exist()
    {
        var provider = new PromptProfileProvider(
            new Mock<ILogger<PromptProfileProvider>>().Object);

        var profiles = provider.GetEnabledProfilesAsync().Result;

        Assert.NotEmpty(profiles);
        Assert.Contains(profiles, p => p.Name == "DefaultDeveloper");
    }

    [Fact]
    public void PromptProfileProvider_CreateProfile_ReturnsNewId()
    {
        var provider = new PromptProfileProvider(
            new Mock<ILogger<PromptProfileProvider>>().Object);

        var profile = provider.CreateAsync(new PromptProfile
        {
            Name = "TestProfile",
            Description = "Test"
        }).Result;

        Assert.NotEqual(Guid.Empty, profile.Id);
        Assert.Equal("TestProfile", profile.Name);
    }
}

public class HybridIntentAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_DeterministicOnly_ReturnsResult()
    {
        var deterministic = new DeterministicIntentAnalyzer();
        var mockLlm = new Mock<LlmIntentAnalyzer>(
            Mock.Of<IHttpClientFactory>(),
            Microsoft.Extensions.Options.Options.Create(new MemoryIntelligenceOptions { Enabled = false }),
            Mock.Of<ILogger<LlmIntentAnalyzer>>());
        var mockResolver = new Mock<IIntentResolver>();
        mockResolver.Setup(r => r.Resolve(It.IsAny<IntentAnalysisResult>(), It.IsAny<IntentAnalysisResult?>()))
            .Returns((IntentAnalysisResult d, IntentAnalysisResult? l) => d);

        var analyzer = new HybridIntentAnalyzer(
            deterministic,
            mockLlm.Object,
            mockResolver.Object,
            Mock.Of<ILogger<HybridIntentAnalyzer>>());

        var result = await analyzer.AnalyzeAsync("Fix this EF Core error");

        Assert.Equal(IntentType.Debugging, result.Intent);
    }
}
