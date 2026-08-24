namespace DeveloperMemory.Application.Exceptions;

public class MemoryNotFoundException : DomainException
{
    public MemoryNotFoundException(Guid id)
        : base($"Memory entry with ID '{id}' was not found.", "memory_not_found")
    {
    }
}
