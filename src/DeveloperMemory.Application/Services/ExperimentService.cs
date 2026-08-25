using System.Security.Cryptography;
using System.Text;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Manages prompt experiments: lifecycle, deterministic assignment, and result recording.
/// Backed by IPromptExperimentRepository for persistence.
/// </summary>
public class ExperimentService : IExperimentService
{
    private readonly IPromptExperimentRepository _experimentRepository;
    private readonly IPromptIntelligenceAudit? _audit;
    private readonly ILogger<ExperimentService> _logger;

    public ExperimentService(
        IPromptExperimentRepository experimentRepository,
        ILogger<ExperimentService> logger,
        IPromptIntelligenceAudit? audit = null)
    {
        _experimentRepository = experimentRepository;
        _logger = logger;
        _audit = audit;
    }

    // ═══════════════════════════════════════════════════════════════
    // EXPERIMENT CRUD
    // ═══════════════════════════════════════════════════════════════

    public async Task<PromptExperiment> CreateExperimentAsync(
        PromptExperiment experiment,
        List<PromptExperimentVariant> variants,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(experiment.Name))
            throw new ArgumentException("Experiment name is required.");

        if (variants.Count < 2)
            throw new ArgumentException("At least two variants are required for an experiment.");

        // Validate weights sum
        var totalWeight = variants.Sum(v => v.Weight);
        if (totalWeight <= 0)
            throw new ArgumentException("Variant weights must sum to a positive value.");

        // Create experiment
        var created = await _experimentRepository.CreateExperimentAsync(experiment, ct);

        // Add variants
        foreach (var variant in variants)
        {
            variant.ExperimentId = created.Id;
            await _experimentRepository.AddVariantAsync(variant, ct);
        }

        // Audit
        await RecordAuditEventAsync("ExperimentCreated",
            $"Experiment '{created.Name}' created with {variants.Count} variants",
            created.Id, ct);

        _logger.LogInformation(
            "Experiment created: {Name} ({Id}) with {VariantCount} variants",
            created.Name, created.Id, variants.Count);

        return created;
    }

    public async Task<PromptExperiment?> GetExperimentAsync(Guid id, CancellationToken ct = default)
    {
        return await _experimentRepository.GetByIdAsync(id, ct);
    }

    public async Task<IReadOnlyList<PromptExperiment>> GetExperimentsAsync(
        ExperimentStatus? status = null,
        CancellationToken ct = default)
    {
        return await _experimentRepository.ListAsync(status, ct);
    }

    public async Task<IReadOnlyList<PromptExperimentVariant>> GetVariantsAsync(
        Guid experimentId,
        CancellationToken ct = default)
    {
        return await _experimentRepository.GetVariantsAsync(experimentId, ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE MANAGEMENT
    // ═══════════════════════════════════════════════════════════════

    public async Task<bool> StartExperimentAsync(Guid experimentId, CancellationToken ct = default)
    {
        var experiment = await _experimentRepository.GetByIdAsync(experimentId, ct);
        if (experiment == null) return false;

        if (experiment.Status != ExperimentStatus.Draft)
        {
            _logger.LogWarning(
                "Cannot start experiment {Id}: current status is {Status} (must be Draft)",
                experimentId, experiment.Status);
            return false;
        }

        // Must have at least one enabled variant
        var enabledVariants = await _experimentRepository.GetEnabledVariantsAsync(experimentId, ct);
        if (enabledVariants.Count == 0)
        {
            _logger.LogWarning("Cannot start experiment {Id}: no enabled variants", experimentId);
            return false;
        }

        experiment.Status = ExperimentStatus.Running;
        experiment.StartAt = DateTime.UtcNow;
        await _experimentRepository.UpdateAsync(experiment, ct);

        await RecordAuditEventAsync("ExperimentStarted",
            $"Experiment '{experiment.Name}' started with {enabledVariants.Count} enabled variants",
            experimentId, ct);

        _logger.LogInformation("Experiment started: {Id}", experimentId);
        return true;
    }

    public async Task<bool> PauseExperimentAsync(Guid experimentId, CancellationToken ct = default)
    {
        var experiment = await _experimentRepository.GetByIdAsync(experimentId, ct);
        if (experiment == null) return false;

        if (experiment.Status != ExperimentStatus.Running)
        {
            _logger.LogWarning(
                "Cannot pause experiment {Id}: current status is {Status} (must be Running)",
                experimentId, experiment.Status);
            return false;
        }

        experiment.Status = ExperimentStatus.Paused;
        await _experimentRepository.UpdateAsync(experiment, ct);

        await RecordAuditEventAsync("ExperimentPaused",
            $"Experiment '{experiment.Name}' paused",
            experimentId, ct);

        _logger.LogInformation("Experiment paused: {Id}", experimentId);
        return true;
    }

    public async Task<bool> CompleteExperimentAsync(Guid experimentId, CancellationToken ct = default)
    {
        var experiment = await _experimentRepository.GetByIdAsync(experimentId, ct);
        if (experiment == null) return false;

        if (experiment.Status != ExperimentStatus.Running &&
            experiment.Status != ExperimentStatus.Paused)
        {
            _logger.LogWarning(
                "Cannot complete experiment {Id}: current status is {Status} (must be Running or Paused)",
                experimentId, experiment.Status);
            return false;
        }

        experiment.Status = ExperimentStatus.Completed;
        experiment.EndAt = DateTime.UtcNow;
        await _experimentRepository.UpdateAsync(experiment, ct);

        await RecordAuditEventAsync("ExperimentCompleted",
            $"Experiment '{experiment.Name}' completed",
            experimentId, ct);

        _logger.LogInformation("Experiment completed: {Id}", experimentId);
        return true;
    }

    public async Task<bool> CancelExperimentAsync(Guid experimentId, CancellationToken ct = default)
    {
        var experiment = await _experimentRepository.GetByIdAsync(experimentId, ct);
        if (experiment == null) return false;

        if (experiment.Status == ExperimentStatus.Completed ||
            experiment.Status == ExperimentStatus.Cancelled)
        {
            _logger.LogWarning(
                "Cannot cancel experiment {Id}: current status is {Status} (terminal state)",
                experimentId, experiment.Status);
            return false;
        }

        experiment.Status = ExperimentStatus.Cancelled;
        experiment.EndAt = DateTime.UtcNow;
        await _experimentRepository.UpdateAsync(experiment, ct);

        await RecordAuditEventAsync("ExperimentCancelled",
            $"Experiment '{experiment.Name}' cancelled",
            experimentId, ct);

        _logger.LogInformation("Experiment cancelled: {Id}", experimentId);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    // DETERMINISTIC ASSIGNMENT
    // ═══════════════════════════════════════════════════════════════

    public async Task<ExperimentAssignmentResult?> AssignAsync(
        Guid experimentId,
        string stableKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(stableKey))
            throw new ArgumentException("Stable key is required for assignment.");

        var experiment = await _experimentRepository.GetByIdAsync(experimentId, ct);
        if (experiment == null) return null;

        // Only Running experiments can create new assignments
        if (experiment.Status != ExperimentStatus.Running)
        {
            _logger.LogDebug(
                "Experiment {Id} is not Running (status: {Status}), cannot create new assignments",
                experimentId, experiment.Status);
            return null;
        }

        // Compute hash of stable key
        var keyHash = ComputeKeyHash(stableKey);

        // Check for existing assignment (reuse for determinism)
        var existing = await _experimentRepository.GetAssignmentAsync(experimentId, keyHash, ct);
        if (existing != null)
        {
            var existingVariant = (await _experimentRepository.GetVariantsAsync(experimentId, ct))
                .FirstOrDefault(v => v.Id == existing.VariantId);

            if (existingVariant != null)
            {
                _logger.LogDebug(
                    "Reusing existing assignment for experiment {ExperimentId}, key hash {Hash[..16]}",
                    experimentId, keyHash[..16]);

                return new ExperimentAssignmentResult
                {
                    Variant = existingVariant,
                    Experiment = experiment,
                    AssignmentKeyHash = keyHash
                };
            }
        }

        // Get enabled variants for new assignment
        var enabledVariants = await _experimentRepository.GetEnabledVariantsAsync(experimentId, ct);
        if (enabledVariants.Count == 0)
        {
            _logger.LogWarning("No enabled variants for experiment {Id}", experimentId);
            return null;
        }

        // Deterministic weighted selection
        var selectedVariantId = SelectVariant(enabledVariants, stableKey, experimentId);
        var selectedVariant = enabledVariants.First(v => v.Id == selectedVariantId);

        // Persist assignment
        var assignment = new PromptExperimentAssignment
        {
            ExperimentId = experimentId,
            VariantId = selectedVariantId,
            AssignmentKeyHash = keyHash
        };

        await _experimentRepository.CreateAssignmentAsync(assignment, ct);

        await RecordAuditEventAsync("ExperimentAssigned",
            $"Key assigned to variant '{selectedVariant.Name}'",
            experimentId, ct);

        return new ExperimentAssignmentResult
        {
            Variant = selectedVariant,
            Experiment = experiment,
            AssignmentKeyHash = keyHash
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // RESULT RECORDING
    // ═══════════════════════════════════════════════════════════════

    public async Task RecordResultAsync(
        PromptExperimentResult result,
        CancellationToken ct = default)
    {
        result.Id = result.Id == Guid.Empty ? Guid.NewGuid() : result.Id;
        result.CreatedAt = DateTime.UtcNow;

        await _experimentRepository.RecordResultAsync(result, ct);

        _logger.LogDebug(
            "Experiment result recorded: Experiment={ExperimentId}, Variant={VariantId}, Quality={QualityScore}",
            result.ExperimentId, result.VariantId, result.QualityScore);
    }

    public async Task<IReadOnlyList<PromptExperimentResult>> GetResultsAsync(
        Guid experimentId,
        Guid? variantId = null,
        CancellationToken ct = default)
    {
        return await _experimentRepository.GetResultsAsync(experimentId, variantId, ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // DETERMINISTIC VARIANT SELECTION
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Computes a deterministic variant assignment using stable hash.
    /// Algorithm:
    ///   1. Compute SHA-256(experimentId:stableKey) to get an integer hash.
    ///   2. Normalize to [0, 1) range.
    ///   3. Walk enabled variants in order, accumulating weight.
    ///   4. Select the variant whose cumulative weight range contains the hash.
    /// Same key + same experiment = same variant, always.
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
    /// Computes a SHA-256 hash of a stable assignment key.
    /// Used for deterministic experiment assignment.
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

    private async Task RecordAuditEventAsync(
        string eventType, string details, Guid experimentId, CancellationToken ct)
    {
        if (_audit == null) return;

        try
        {
            await _audit.RecordEventAsync(new PromptAuditEvent
            {
                EventType = eventType,
                Details = details,
                CorrelationId = experimentId.ToString()
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record audit event {EventType}", eventType);
        }
    }
}
