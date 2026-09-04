namespace DeveloperMemory.Application.Exceptions;

/// <summary>
/// Thrown when a decomposition plan fails validation (empty, excessive task
/// count, unknown/disabled agents, invalid dependencies, cycles, or recursive
/// delegation instructions). Callers may fall back to direct execution.
/// </summary>
public class InvalidTaskDecompositionException : DomainException
{
    /// <summary>Short machine-readable reason for the rejection.</summary>
    public string Reason { get; }

    public InvalidTaskDecompositionException(string reason)
        : base($"Task decomposition rejected: {reason}", "invalid_decomposition")
    {
        Reason = reason;
    }
}