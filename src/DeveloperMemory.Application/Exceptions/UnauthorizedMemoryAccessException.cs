namespace DeveloperMemory.Application.Exceptions;

/// <summary>
/// Thrown when a user attempts to access a memory they do not own.
/// In API responses, this maps to 404 Not Found to prevent information leakage.
/// </summary>
public class UnauthorizedMemoryAccessException : DomainException
{
    public Guid MemoryId { get; }

    public UnauthorizedMemoryAccessException(Guid memoryId)
        : base("The requested memory was not found.", "memory_not_found")
    {
        MemoryId = memoryId;
    }
}
