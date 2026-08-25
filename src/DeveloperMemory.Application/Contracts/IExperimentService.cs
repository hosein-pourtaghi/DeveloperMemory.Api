using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Service for managing prompt experiments.
/// Handles experiment lifecycle, assignment, and result recording.
/// </summary>
public interface IExperimentService
{
    /// <summary>
    /// Creates a new experiment.
    /// </summary>
    Task<PromptExperiment> CreateExperimentAsync(
        PromptExperiment experiment,
        List<PromptExperimentVariant> variants,
        CancellationToken ct = default);

    /// <summary>
    /// Gets an experiment by ID.
    /// </summary>
    Task<PromptExperiment?> GetExperimentAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets all experiments.
    /// </summary>
    Task<IReadOnlyList<PromptExperiment>> GetExperimentsAsync(
        ExperimentStatus? status = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets variants for an experiment.
    /// </summary>
    Task<IReadOnlyList<PromptExperimentVariant>> GetVariantsAsync(
        Guid experimentId,
        CancellationToken ct = default);

    /// <summary>
    /// Starts an experiment.
    /// </summary>
    Task<bool> StartExperimentAsync(Guid experimentId, CancellationToken ct = default);

    /// <summary>
    /// Pauses an experiment.
    /// </summary>
    Task<bool> PauseExperimentAsync(Guid experimentId, CancellationToken ct = default);

    /// <summary>
    /// Completes an experiment.
    /// </summary>
    Task<bool> CompleteExperimentAsync(Guid experimentId, CancellationToken ct = default);

    /// <summary>
    /// Cancels an experiment.
    /// </summary>
    Task<bool> CancelExperimentAsync(Guid experimentId, CancellationToken ct = default);

    /// <summary>
    /// Assigns a stable key to a variant deterministically.
    /// Returns null if no active experiment or assignment fails.
    /// </summary>
    Task<ExperimentAssignmentResult?> AssignAsync(
        Guid experimentId,
        string stableKey,
        CancellationToken ct = default);

    /// <summary>
    /// Records a processing result for an experiment.
    /// </summary>
    Task RecordResultAsync(
        PromptExperimentResult result,
        CancellationToken ct = default);

    /// <summary>
    /// Gets results for an experiment.
    /// </summary>
    Task<IReadOnlyList<PromptExperimentResult>> GetResultsAsync(
        Guid experimentId,
        Guid? variantId = null,
        CancellationToken ct = default);
}

/// <summary>
/// Result of an experiment assignment.
/// </summary>
public class ExperimentAssignmentResult
{
    /// <summary>The assigned variant.</summary>
    public PromptExperimentVariant Variant { get; set; } = null!;

    /// <summary>The experiment.</summary>
    public PromptExperiment Experiment { get; set; } = null!;

    /// <summary>Hash of the assignment key.</summary>
    public string AssignmentKeyHash { get; set; } = string.Empty;
}

/// <summary>
/// Service for orchestrating prompt quality comparison and selection.
/// </summary>
public interface IPromptCandidateSelector
{
    /// <summary>
    /// Compares prompt candidates and selects the best one.
    /// Deterministic when scores tie.
    /// </summary>
    Task<PromptComparisonResult> CompareAndSelectAsync(
        PromptComparisonRequest request,
        CancellationToken ct = default);
}
