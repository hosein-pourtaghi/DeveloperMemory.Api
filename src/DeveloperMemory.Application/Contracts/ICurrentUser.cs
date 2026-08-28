namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Abstraction for the current authenticated user identity.
/// Resolved from the HTTP request by the API layer.
/// Domain and Application layers depend on this interface, not on ASP.NET Core types.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Whether the current request is authenticated.</summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// The unique identifier for the current user.
    /// Derived server-side from the authenticated principal (e.g., API key mapping).
    /// Never trust client-supplied values for this.
    /// </summary>
    string UserId { get; }

    /// <summary>Optional display name for logging.</summary>
    string? DisplayName { get; }
}
