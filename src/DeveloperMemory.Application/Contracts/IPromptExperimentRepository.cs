using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Provider-independent repository for prompt experiments.
/// Manages experiment lifecycle, variants, and assignments.
/// </summary>
public interface IPromptExperimentRepository
{
    // ── Experiment operations ──

    /// <summary>Creates a new experiment.</summary>
    Task<PromptExperiment> CreateExperimentAsync(PromptExperiment experiment, CancellationToken ct = default);

    /// <summary>Gets an experiment by ID.</summary>
    Task<PromptExperiment?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Gets an experiment by name.</summary>
    Task<PromptExperiment?> GetByNameAsync(string name, CancellationToken ct = default);

    /// <summary>Lists experiments with optional status filter.</summary>
    Task<IReadOnlyList<PromptExperiment>> ListAsync(ExperimentStatus? status = null, CancellationToken ct = default);

    /// <summary>Updates an experiment.</summary>
    Task UpdateAsync(PromptExperiment experiment, CancellationToken ct = default);

    /// <summary>Checks if an experiment exists.</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Gets all running experiments.</summary>
    Task<IReadOnlyList<PromptExperiment>> GetRunningAsync(CancellationToken ct = default);

    // ── Variant operations ──

    /// <summary>Adds a variant to an experiment.</summary>
    Task<PromptExperimentVariant> AddVariantAsync(PromptExperimentVariant variant, CancellationToken ct = default);

    /// <summary>Updates a variant.</summary>
    Task UpdateVariantAsync(PromptExperimentVariant variant, CancellationToken ct = default);

    /// <summary>Gets all variants for an experiment.</summary>
    Task<IReadOnlyList<PromptExperimentVariant>> GetVariantsAsync(Guid experimentId, CancellationToken ct = default);

    /// <summary>Gets enabled variants for an experiment.</summary>
    Task<IReadOnlyList<PromptExperimentVariant>> GetEnabledVariantsAsync(Guid experimentId, CancellationToken ct = default);

    /// <summary>Enables a variant.</summary>
    Task SetVariantEnabledAsync(Guid variantId, bool enabled, CancellationToken ct = default);

    // ── Assignment operations ──

    /// <summary>Gets an existing assignment for an experiment and key hash.</summary>
    Task<PromptExperimentAssignment?> GetAssignmentAsync(Guid experimentId, string assignmentKeyHash, CancellationToken ct = default);

    /// <summary>Creates a new assignment.</summary>
    Task<PromptExperimentAssignment> CreateAssignmentAsync(PromptExperimentAssignment assignment, CancellationToken ct = default);

    // ── Result operations ──

    /// <summary>Records an experiment result.</summary>
    Task<PromptExperimentResult> RecordResultAsync(PromptExperimentResult result, CancellationToken ct = default);

    /// <summary>Gets results for an experiment, optionally filtered by variant.</summary>
    Task<IReadOnlyList<PromptExperimentResult>> GetResultsAsync(Guid experimentId, Guid? variantId = null, CancellationToken ct = default);

    /// <summary>Gets results within a time range.</summary>
    Task<IReadOnlyList<PromptExperimentResult>> GetResultsByTimeRangeAsync(
        Guid experimentId, DateTime from, DateTime to, CancellationToken ct = default);
}
