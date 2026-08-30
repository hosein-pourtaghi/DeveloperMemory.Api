using DeveloperMemory.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Hybrid conversational memory detector that combines deterministic pattern matching
/// with LLM-assisted semantic analysis for ambiguous messages.
///
/// Strategy:
///   1. Run deterministic detection first (cheap, fast)
///   2. If confidence is high (>0.7) → use deterministic result
///   3. If confidence is low (<0.3) → skip (not durable)
///   4. If confidence is ambiguous (0.3-0.7) → delegate to LLM analysis
///   5. If LLM is unavailable → conservatively reject ambiguous messages
///
/// This ensures:
///   - Clear messages are handled cheaply without LLM calls
///   - Ambiguous messages get semantic analysis
///   - LLM unavailability degrades gracefully
///   - No message ever requires an LLM call to be processed
/// </summary>
public class HybridConversationalMemoryDetector : IConversationalMemoryDetector
{
    private readonly ConversationalMemoryDetector _deterministicDetector;
    private readonly ISemanticContextAnalyzer? _semanticAnalyzer;
    private readonly ILogger<HybridConversationalMemoryDetector> _logger;

    /// <summary>
    /// Confidence threshold above which deterministic results are accepted as-is.
    /// </summary>
    private const double HighConfidenceThreshold = 0.7;

    /// <summary>
    /// Confidence threshold below which messages are conservatively rejected.
    /// </summary>
    private const double LowConfidenceThreshold = 0.3;

    public HybridConversationalMemoryDetector(
        ConversationalMemoryDetector deterministicDetector,
        ILogger<HybridConversationalMemoryDetector> logger,
        ISemanticContextAnalyzer? semanticAnalyzer = null)
    {
        _deterministicDetector = deterministicDetector;
        _logger = logger;
        _semanticAnalyzer = semanticAnalyzer;
    }

    public ConversationalMemoryDetectionResult Detect(
        string message, List<string>? conversationContext = null)
    {
        // Step 1: Run deterministic detection first (always available, always fast)
        var deterministicResult = _deterministicDetector.Detect(message, conversationContext);

        // Step 2: If the deterministic result is clear, use it directly
        if (deterministicResult.Confidence >= HighConfidenceThreshold ||
            deterministicResult.Confidence <= LowConfidenceThreshold)
        {
            _logger.LogDebug(
                "Deterministic detection sufficient: detected={Detected}, confidence={Confidence:F2}",
                deterministicResult.ContainsDurableInformation, deterministicResult.Confidence);
            return deterministicResult;
        }

        // Step 3: Ambiguous case — try LLM-assisted analysis
        if (_semanticAnalyzer == null)
        {
            _logger.LogDebug(
                "Ambiguous message (confidence={Confidence:F2}) but no LLM analyzer available; conservatively rejecting",
                deterministicResult.Confidence);
            return deterministicResult;
        }

        // Step 4: Synchronously run LLM analysis for ambiguous messages
        // This is acceptable because ambiguous messages are rare in practice
        try
        {
            var semanticResult = _semanticAnalyzer
                .AnalyzeAsync(message, conversationContext, null)
                .GetAwaiter()
                .GetResult();

            if (semanticResult != null && semanticResult.ContainsDurableInformation)
            {
                _logger.LogInformation(
                    "LLM resolved ambiguous message: detected={Detected}, confidence={Confidence:F2}, reason={Reason}",
                    true, semanticResult.Confidence, semanticResult.Reason);

                return new ConversationalMemoryDetectionResult
                {
                    ContainsDurableInformation = true,
                    Confidence = Math.Max(semanticResult.Confidence, 0.5),
                    Reason = $"LLM analysis: {semanticResult.Reason}",
                    SuggestedMemoryType = semanticResult.SuggestedMemoryType,
                    ExtractedContent = semanticResult.ExtractedContent
                };
            }

            // LLM says not durable — use LLM's confidence to override
            _logger.LogDebug(
                "LLM determined message is not durable: reason={Reason}",
                semanticResult?.Reason ?? "no reason");
            return new ConversationalMemoryDetectionResult
            {
                ContainsDurableInformation = false,
                Confidence = semanticResult?.Confidence ?? deterministicResult.Confidence,
                Reason = $"LLM analysis: {semanticResult?.Reason ?? "not durable"}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM analysis failed for ambiguous message; using deterministic result");
            return deterministicResult;
        }
    }
}
