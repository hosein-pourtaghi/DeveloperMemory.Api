namespace DeveloperMemory.Application.Configuration;

/// <summary>
/// Bounds for V2-4 task decomposition and delegation.
///
/// Delegation is deliberately bounded and synchronous: maximum delegation
/// depth is always 1 (subtasks are executed as direct assistant turns and can
/// never decompose further).
/// </summary>
public class TaskDecompositionOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "TaskDecomposition";

    /// <summary>Model used for semantic decomposition.</summary>
    public string DecompositionModel { get; set; } = "auto";

    /// <summary>Maximum number of subtasks in one plan.</summary>
    public int MaxSubtasks { get; set; } = 5;

    /// <summary>Maximum character length of a subtask description.</summary>
    public int MaxDescriptionLength { get; set; } = 2000;

    /// <summary>Maximum number of dependencies per subtask.</summary>
    public int MaxDependenciesPerTask { get; set; } = 5;

    /// <summary>Decomposition model request timeout in seconds.</summary>
    public int DecompositionTimeoutSeconds { get; set; } = 60;

    /// <summary>Overall delegated execution timeout in seconds.</summary>
    public int DelegationTimeoutSeconds { get; set; } = 300;
}