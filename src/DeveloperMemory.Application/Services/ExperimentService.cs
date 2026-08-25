using System.Security.Cryptography;
using System.Text;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Manages prompt experiments: lifecycle, deterministic assignment, and result recording.
/// </summary>
public class ExperimentService : IExperimentService
{
    private readonly IPromptProfileProvider _profileProvider;
    private readonly ILogger<ExperimentService> _logger;

    public ExperimentService(
        IPromptProfileProvider profileProvider,
        ILogger<ExperimentService> logger)
    {
        _profileProvider = profileProvider;
        _logger = logger;
    }

    public async Task<PromptExperiment> CreateExperimentAsync(
        PromptExperiment experiment,
        List<PromptExperimentVariant> variants,
        CancellationToken ct = default)
    {
        experiment.Id = experiment.Id == Guid.Empty ? Guid.NewGuid() : experiment.Id;
        experiment.CreatedAt = DateTime.UtcNow;
        experiment.UpdatedAt = DateTime.UtcNow;
        experiment.Status = ExperimentStatus.Draft;

        foreach (var variant in variants)
        {
            variant.Id = variant.Id == Guid.Empty ? Guid.NewGuid() : variant.Id;
            variant.ExperimentId = experiment.Id;
            variant.CreatedAt = DateTime.UtcNow;
        }

        _logger.LogInformation(
            "Experiment created: {Name} ({VariantCount} variants)",
            experiment.Name, variants.Count);

        return await Task.FromResult(experiment);
    }

    public Task<PromptExperiment?> GetExperimentAsync(Guid id, CancellationToken ct = default)
    {
        // In production this would query the database
        return Task.FromResult<PromptExperiment?>(null);
    }

    public Task<IReadOnlyList<PromptExperiment>> GetExperimentsAsync(
        ExperimentStatus? status = null,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<PromptExperiment>>([]);
    }

    public Task<IReadOnlyList<PromptExperimentVariant>> GetVariantsAsync(
        Guid experimentId,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<PromptExperimentVariant>>([]);
    }

    public Task<bool> StartExperimentAsync(Guid experimentId, CancellationToken ct = default)
    {
        _logger.LogInformation("Experiment started: {Id}", experimentId);
        return Task.FromResult(true);
    }

    public Task<bool> PauseExperimentAsync(Guid experimentId, CancellationToken ct = default)
    {
        _logger.LogInformation("Experiment paused: {Id}", experimentId);
        return Task.FromResult(true);
    }

    public Task<bool> CompleteExperimentAsync(Guid experimentId, CancellationToken ct = default)
    {
        _logger.LogInformation("Experiment completed: {Id}", experimentId);
        return Task.FromResult(true);
    }

    public Task<bool> CancelExperimentAsync(Guid experimentId, CancellationToken ct = default)
    {
        _logger.LogInformation("Experiment cancelled: {Id}", experimentId);
        return Task.FromResult(true);
    }

    public Task<ExperimentAssignmentResult?> AssignAsync(
        Guid experimentId,
        string stableKey,
        CancellationToken ct = default)
    {
        // Deterministic hash-based assignment
        var keyHash = ComputeKeyHash(stableKey);
        _logger.LogDebug("Assignment computed for key hash: {Hash}", keyHash[..16]);

        // In production, this would load variants and select based on weight
        // For now return a placeholder
        return Task.FromResult<ExperimentAssignmentResult?>(null);
    }

    public Task RecordResultAsync(
        PromptExperimentResult result,
        CancellationToken ct = default)
    {
        result.Id = result.Id == Guid.Empty ? Guid.NewGuid() : result.Id;
        result.CreatedAt = DateTime.UtcNow;

        _logger.LogDebug(
            "Experiment result recorded: Experiment={ExperimentId}, Variant={VariantId}, Quality={QualityScore}",
            result.ExperimentId, result.VariantId, result.QualityScore);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PromptExperimentResult>> GetResultsAsync(
        Guid experimentId,
        Guid? variantId = null,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<PromptExperimentResult>>([]);
    }

    /// <summary>
    /// Computes a deterministic variant assignment using stable hash.
    /// Same key → same variant always.
    /// </summary>
    public static Guid SelectVariant(
        IReadOnlyList<PromptExperimentVariant> variants,
        string stableKey,
        Guid experimentId)
    {
        if (variants.Count == 0)
            throw new ArgumentException("No variants available for assignment");

        var enabledVariants = variants.Where(v => v.Enabled).ToList();
        if (enabledVariants.Count == 0)
            throw new InvalidOperationException("No enabled variants available");

        // Compute stable hash
        var hash = ComputeDeterministicHash(stableKey, experimentId);

        // Weighted selection using hash
        var totalWeight = enabledVariants.Sum(v => v.Weight);
        var normalizedPosition = (double)(hash % 10000) / 10000.0;

        double cumulative = 0;
        foreach (var variant in enabledVariants)
        {
            cumulative += variant.Weight / totalWeight;
            if (normalizedPosition < cumulative)
                return variant.Id;
        }

        // Fallback to last variant (deterministic)
        return enabledVariants[^1].Id;
    }

    /// <summary>
    /// Computes a deterministic hash for stable assignment.
    /// SHA-256 of (experimentId + stableKey).
    /// </summary>
    public static string ComputeKeyHash(string stableKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(stableKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static int ComputeDeterministicHash(string stableKey, Guid experimentId)
    {
        var input = $"{experimentId}:{stableKey}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Math.Abs(BitConverter.ToInt32(bytes, 0));
    }
}
