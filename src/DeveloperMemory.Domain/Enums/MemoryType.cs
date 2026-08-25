namespace DeveloperMemory.Domain.Enums;

/// <summary>
/// Classification of memory by its semantic type.
/// Extensible — new types can be added without changing the core model.
/// </summary>
public enum MemoryType
{
    /// <summary>User preference or habit (e.g., "prefers PostgreSQL").</summary>
    UserPreference,

    /// <summary>User goal or objective (e.g., "finish Phase 5").</summary>
    UserGoal,

    /// <summary>User constraint or rule (e.g., "no paid services").</summary>
    UserConstraint,

    /// <summary>Project-level context (e.g., "uses Clean Architecture").</summary>
    ProjectContext,

    /// <summary>Architecture decision (e.g., "selected CQRS pattern").</summary>
    ArchitectureDecision,

    /// <summary>Technical decision (e.g., "using EF Core for persistence").</summary>
    TechnicalDecision,

    /// <summary>Current working context (e.g., "implementing Phase 5").</summary>
    WorkingContext,

    /// <summary>Explicit instruction (e.g., "always run tests before commit").</summary>
    Instruction,

    /// <summary>Objective fact (e.g., "the API uses .NET 10").</summary>
    Fact,

    /// <summary>Conversation or session context.</summary>
    ConversationContext,

    /// <summary>General or unclassified memory.</summary>
    Other
}
