namespace DeveloperMemory.Domain.Enums;

/// <summary>
/// Categories of constraints that the Prompt Intelligence Engine should respect.
/// </summary>
public enum ConstraintType
{
    /// <summary>Technology stack constraints (e.g., use PostgreSQL, use .NET 10).</summary>
    Technology,

    /// <summary>Architecture constraints (e.g., keep provider-independent, use Clean Architecture).</summary>
    Architecture,

    /// <summary>Cost constraints (e.g., use free services only, no paid APIs).</summary>
    Cost,

    /// <summary>Security constraints (e.g., no secrets in logs, encrypt at rest).</summary>
    Security,

    /// <summary>Scope constraints (e.g., only modify the Application layer).</summary>
    Scope,

    /// <summary>Output format constraints (e.g., return JSON, use XML docs).</summary>
    OutputFormat,

    /// <summary>Performance constraints (e.g., sub-100ms latency, avoid N+1 queries).</summary>
    Performance,

    /// <summary>Compatibility constraints (e.g., must work with .NET 8, target net10.0).</summary>
    Compatibility,

    /// <summary>Implementation preferences (e.g., prefer async, use dependency injection).</summary>
    Implementation,

    /// <summary>User-stated preferences from conversation or profile.</summary>
    UserPreference,

    /// <summary>Project-level rules from project configuration or persistent memory.</summary>
    ProjectRule
}
