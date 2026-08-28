using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Configuration;
using DeveloperMemory.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;


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
