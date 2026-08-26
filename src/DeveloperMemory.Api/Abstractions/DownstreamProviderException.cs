using System.Net;

namespace DeveloperMemory.Api.Abstractions;

/// <summary>
/// Exception thrown when a downstream model provider returns an error response.
/// Carries the provider's status code and raw error content for translation
/// into appropriate API error responses.
/// </summary>
public class DownstreamProviderException : Exception
{
    /// <summary>
    /// The HTTP status code returned by the downstream provider.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// The raw error content from the downstream provider (typically JSON).
    /// </summary>
    public string RawErrorContent { get; }

    public DownstreamProviderException(HttpStatusCode statusCode, string rawErrorContent)
        : base($"Downstream provider returned {statusCode}: {rawErrorContent}")
    {
        StatusCode = statusCode;
        RawErrorContent = rawErrorContent;
    }
}
