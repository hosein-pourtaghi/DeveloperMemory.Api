namespace DeveloperMemory.Domain.Enums;

/// <summary>
/// The high-level intent of the user's request.
/// Used by the Prompt Intelligence Engine for context selection and organization.
/// </summary>
public enum IntentType
{
    /// <summary>Request involves writing, modifying, or generating code.</summary>
    Coding,

    /// <summary>Request involves diagnosing and fixing issues.</summary>
    Debugging,

    /// <summary>Request involves system design, architecture decisions, or structural planning.</summary>
    Architecture,

    /// <summary>Request involves creating or editing documentation.</summary>
    Documentation,

    /// <summary>Request involves researching a topic or gathering information.</summary>
    Research,

    /// <summary>Request asks for explanation of concepts or behavior.</summary>
    Explanation,

    /// <summary>Request involves restructuring existing code without changing behavior.</summary>
    Refactoring,

    /// <summary>Request involves planning future work or organizing tasks.</summary>
    Planning,

    /// <summary>General-purpose request that does not fit specific categories.</summary>
    General
}
