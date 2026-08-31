namespace DeveloperMemory.Domain.Enums;

/// <summary>
/// Classification of agent making the request.
/// Used by the Agent Context Intelligence layer to understand agent intent.
/// </summary>
public enum AgentType
{
    /// <summary>General-purpose agent (default).</summary>
    General,

    /// <summary>Agent performing code generation, modification, or analysis.</summary>
    Coding,

    /// <summary>Agent creating or editing documentation.</summary>
    Documentation,

    /// <summary>Agent performing planning, task breakdown, or architecture.</summary>
    Planning,

    /// <summary>Agent writing, running, or analyzing tests.</summary>
    Testing,

    /// <summary>Agent performing deployment, CI/CD, or infrastructure tasks.</summary>
    DevOps
}
