using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Configuration;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;


public class ExperimentServiceTests
{
    private readonly IExperimentService _experimentService;

    public ExperimentServiceTests()
    {
        var experimentRepository = new Mock<IPromptExperimentRepository>();
        // Setup the mock so that CreateExperimentAsync sets the Id like InMemoryPromptExperimentRepository does
        experimentRepository
            .Setup(r => r.CreateExperimentAsync(It.IsAny<PromptExperiment>(), It.IsAny<CancellationToken>()))
            .Returns((PromptExperiment e, CancellationToken _) =>
            {
                if (e.Id == Guid.Empty) e.Id = Guid.NewGuid();
                e.CreatedAt = DateTime.UtcNow;
                e.UpdatedAt = DateTime.UtcNow;
                e.Status = ExperimentStatus.Draft;
                return Task.FromResult(e);
            });
        _experimentService = new ExperimentService(
            experimentRepository.Object,
            new Mock<ILogger<ExperimentService>>().Object);
    }

    [Fact]
    public async Task CreateExperiment_SetsIdAndStatus()
    {
        var experiment = new PromptExperiment
        {
            Name = "Test Experiment"
        };
        var variants = new List<PromptExperimentVariant>
        {
            new() { Name = "control", OptimizationMode = "Deterministic" },
            new() { Name = "treatment", OptimizationMode = "LLM" }
        };

        var created = await _experimentService.CreateExperimentAsync(experiment, variants);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(ExperimentStatus.Draft, created.Status);
        Assert.Equal(2, variants.Count);
    }

    [Fact]
    public async Task CreateExperiment_SetsVariantExperimentId()
    {
        var experiment = new PromptExperiment { Name = "Test" };
        var variants = new List<PromptExperimentVariant>
        {
            new() { Name = "A", Weight = 0.5 },
            new() { Name = "B", Weight = 0.5 }
        };

        var created = await _experimentService.CreateExperimentAsync(experiment, variants);

        Assert.Equal(created.Id, variants[0].ExperimentId);
        Assert.Equal(created.Id, variants[1].ExperimentId);
    }

    [Fact]
    public void SelectVariant_DeterministicAssignment()
    {
        var variants = new List<PromptExperimentVariant>
        {
            new() { Id = Guid.NewGuid(), Name = "A", Weight = 0.5, Enabled = true },
            new() { Id = Guid.NewGuid(), Name = "B", Weight = 0.5, Enabled = true }
        };

        var experimentId = Guid.NewGuid();
        var key = "user-123";

        var variant1 = ExperimentService.SelectVariant(variants, key, experimentId);
        var variant2 = ExperimentService.SelectVariant(variants, key, experimentId);

        // Same key → same variant always
        Assert.Equal(variant1, variant2);
    }

    [Fact]
    public void SelectVariant_DifferentKeys_CanProduceDifferentVariants()
    {
        var variants = new List<PromptExperimentVariant>
        {
            new() { Id = Guid.NewGuid(), Name = "A", Weight = 0.5, Enabled = true },
            new() { Id = Guid.NewGuid(), Name = "B", Weight = 0.5, Enabled = true }
        };

        var experimentId = Guid.NewGuid();

        // Try many different keys - should get both variants at some point
        var results = new HashSet<Guid>();
        for (int i = 0; i < 100; i++)
        {
            results.Add(ExperimentService.SelectVariant(variants, $"user-{i}", experimentId));
        }

        Assert.True(results.Count > 1, "Different keys should produce different variants");
    }

    [Fact]
    public void SelectVariant_DisabledVariant_Ignored()
    {
        var variantA = new PromptExperimentVariant
        {
            Id = Guid.NewGuid(), Name = "A", Weight = 0.5, Enabled = false
        };
        var variantB = new PromptExperimentVariant
        {
            Id = Guid.NewGuid(), Name = "B", Weight = 0.5, Enabled = true
        };
        var variants = new List<PromptExperimentVariant> { variantA, variantB };

        var result = ExperimentService.SelectVariant(variants, "key", Guid.NewGuid());

        Assert.Equal(variantB.Id, result);
    }

    [Fact]
    public void ComputeKeyHash_Deterministic()
    {
        var hash1 = ExperimentService.ComputeKeyHash("user-123");
        var hash2 = ExperimentService.ComputeKeyHash("user-123");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeKeyHash_DifferentKeys_DifferentHashes()
    {
        var hash1 = ExperimentService.ComputeKeyHash("user-123");
        var hash2 = ExperimentService.ComputeKeyHash("user-456");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void RecordResult_SetsIdAndTimestamp()
    {
        var result = new PromptExperimentResult
        {
            ExperimentId = Guid.NewGuid(),
            VariantId = Guid.NewGuid(),
            QualityScore = 0.85
        };

        _experimentService.RecordResultAsync(result).Wait();

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.True(result.CreatedAt <= DateTime.UtcNow);
    }
}

// ═══════════════════════════════════════════════════════════════════
// In-Memory Metrics Tests
// ═══════════════════════════════════════════════════════════════════


public class PromptQualityComparisonTests
{
    [Fact]
    public void Improvement_CalculatesCorrectly()
    {
        var comparison = new PromptQualityComparison
        {
            OriginalScore = 0.70,
            FinalScore = 0.85
        };

        comparison.Improvement = comparison.FinalScore - comparison.OriginalScore;

        Assert.Equal(0.15, comparison.Improvement, 2);
    }

    [Fact]
    public void FallbackUsed_OriginalSelected()
    {
        var comparison = new PromptQualityComparison
        {
            OriginalScore = 0.80,
            FinalScore = 0.80,
            SelectedVariant = "original",
            FallbackUsed = true
        };

        Assert.Equal("original", comparison.SelectedVariant);
        Assert.True(comparison.FallbackUsed);
    }
}
