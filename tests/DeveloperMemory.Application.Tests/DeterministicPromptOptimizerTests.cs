using Microsoft.Extensions.Logging;
using Moq;
using DeveloperMemory.Application.Services.PromptIntelligence;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class DeterministicPromptOptimizerTests
{
    private readonly DeterministicPromptOptimizer _optimizer = new(new Mock<ILogger<DeterministicPromptOptimizer>>().Object);

    [Fact]
    public void Optimize_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _optimizer.Optimize(""));
    }

    [Fact]
    public void Optimize_RemovesDuplicateLines()
    {
        var input = "Line 1\nLine 2\nLine 1\nLine 3";
        var result = _optimizer.Optimize(input);

        var lines = result.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Contains("Line 1", lines[0]);
        Assert.Contains("Line 2", lines[1]);
        Assert.Contains("Line 3", lines[2]);
    }

    [Fact]
    public void Optimize_PreservesSectionHeaders()
    {
        var input = "## Section Header\nContent\n## Section Header\nMore content";
        var result = _optimizer.Optimize(input);

        // Section headers are preserved even if duplicated
        Assert.Contains("## Section Header", result);
    }

    [Fact]
    public void Optimize_PreservesMarkers()
    {
        var input = "--- Marker ---\nContent\n--- Marker ---\nMore";
        var result = _optimizer.Optimize(input);

        Assert.Contains("--- Marker ---", result);
    }

    [Fact]
    public void Optimize_NormalizesWhitespace()
    {
        var input = "Line 1   \n\n\n\n\nLine 2";
        var result = _optimizer.Optimize(input);

        // Should collapse multiple blank lines
        Assert.DoesNotContain("\n\n\n", result);
    }

    [Fact]
    public void Optimize_PreservesUserRequest()
    {
        var input = "User Request: Implement the feature";
        var result = _optimizer.Optimize(input);

        Assert.Contains("User Request: Implement the feature", result);
    }

    [Fact]
    public void Optimize_Deterministic_ForSameInput()
    {
        var input = "Line 1\nLine 2\nLine 1\n\n\n\nLine 3";
        var result1 = _optimizer.Optimize(input);
        var result2 = _optimizer.Optimize(input);

        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Optimize_PreservesContentMeaning()
    {
        var input = "## Project Context\nProject uses PostgreSQL\n\n## Constraints\nUse Clean Architecture";
        var result = _optimizer.Optimize(input);

        Assert.Contains("PostgreSQL", result);
        Assert.Contains("Clean Architecture", result);
    }
}
