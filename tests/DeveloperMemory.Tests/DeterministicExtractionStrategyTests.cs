using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Xunit;

namespace DeveloperMemory.Tests;

public class DeterministicExtractionStrategyTests
{
    private readonly DeterministicExtractionStrategy _strategy = new();

    [Fact]
    public async Task ExtractAsync_EmptyContent_ReturnsEmpty()
    {
        var request = new MemoryExtractionRequest { Content = "" };

        var result = await _strategy.ExtractAsync(request);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractAsync_PreferencePattern_ExtractsPreference()
    {
        var request = new MemoryExtractionRequest
        {
            Content = "I prefer PostgreSQL for the database"
        };

        var result = await _strategy.ExtractAsync(request);

        Assert.Contains(result, c => c.MemoryType == MemoryType.UserPreference);
    }

    [Fact]
    public async Task ExtractAsync_InstructionPattern_ExtractsInstruction()
    {
        var request = new MemoryExtractionRequest
        {
            Content = "Always run tests before committing"
        };

        var result = await _strategy.ExtractAsync(request);

        Assert.Contains(result, c => c.MemoryType == MemoryType.Instruction);
    }

    [Fact]
    public async Task ExtractAsync_FactPattern_ExtractsFact()
    {
        var request = new MemoryExtractionRequest
        {
            Content = "The project uses .NET 10"
        };

        var result = await _strategy.ExtractAsync(request);

        Assert.Contains(result, c => c.MemoryType == MemoryType.Fact);
    }

    [Fact]
    public async Task ExtractAsync_ConstraintPattern_ExtractsConstraint()
    {
        var request = new MemoryExtractionRequest
        {
            Content = "Must not use paid services"
        };

        var result = await _strategy.ExtractAsync(request);

        Assert.Contains(result, c => c.MemoryType == MemoryType.UserConstraint);
    }

    [Fact]
    public async Task ExtractAsync_DeduplicatesByContent()
    {
        var request = new MemoryExtractionRequest
        {
            Content = "prefer PostgreSQL and prefer PostgreSQL"
        };

        var result = await _strategy.ExtractAsync(request);

        // Should not produce duplicate candidates
        var contents = result.Select(c => c.Content).ToList();
        Assert.Equal(contents.Count, contents.Distinct().Count());
    }

    [Fact]
    public async Task ExtractAsync_StrategyName_IsDeterministic()
    {
        Assert.Equal("deterministic", _strategy.StrategyName);
    }
}
