namespace DeveloperMemory.Domain.Enums;

/// <summary>
/// Indicates why a memory was eligible or ineligible for retrieval.
/// Used for explainability and diagnostics.
/// </summary>
public enum RetrievalEligibility
{
    Eligible,
    IneligibleScope,
    IneligibleProjectIsolation,
    IneligibleLifecycle,
    IneligiblePrivacy,
    IneligibleExcludedCategory
}
