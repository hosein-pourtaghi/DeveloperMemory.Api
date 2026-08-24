namespace DeveloperMemory.Application.Exceptions;

public class ProjectNotFoundException : DomainException
{
    public ProjectNotFoundException(Guid id)
        : base($"Project with ID '{id}' was not found.", "project_not_found")
    {
    }
}
