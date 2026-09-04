namespace DeveloperMemory.Application.Exceptions;

/// <summary>
/// Thrown when an agent identifier does not match any registered agent.
/// Maps to a 404-style client error at the API boundary.
/// </summary>
public class AgentNotFoundException : DomainException
{
    public AgentNotFoundException(string agentId)
        : base($"Agent '{agentId}' was not found.", "agent_not_found")
    {
    }
}

/// <summary>
/// Thrown when a registered agent is disabled and cannot execute.
/// Maps to a conflict-style client error at the API boundary.
/// </summary>
public class AgentDisabledException : DomainException
{
    public AgentDisabledException(string agentId)
        : base($"Agent '{agentId}' is disabled.", "agent_disabled")
    {
    }
}