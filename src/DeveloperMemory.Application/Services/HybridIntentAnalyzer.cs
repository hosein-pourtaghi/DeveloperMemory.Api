using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Hybrid intent analyzer combining deterministic and LLM analysis.
/// Deterministic analysis is always the baseline.
/// LLM analysis is optional and enhances classification when available.
///
/// Pipeline:
///   Deterministic analysis (always)
///     +
///   LLM analysis (if available)
///     ↓
///   Intent Resolution
///     ↓
///   Effective Intent
/// </summary>
public class HybridIntentAnalyzer : IIntentAnalyzer
{
    private readonly DeterministicIntentAnalyzer _deterministicAnalyzer;
    private readonly LlmIntentAnalyzer _llmAnalyzer;
    private readonly IIntentResolver _resolver;
    private readonly ILogger<HybridIntentAnalyzer> _logger;

    public HybridIntentAnalyzer(
        DeterministicIntentAnalyzer deterministicAnalyzer,
        LlmIntentAnalyzer llmAnalyzer,
        IIntentResolver resolver,
        ILogger<HybridIntentAnalyzer> logger)
    {
        _deterministicAnalyzer = deterministicAnalyzer;
        _llmAnalyzer = llmAnalyzer;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<IntentAnalysisResult> AnalyzeAsync(
        string input,
        PromptContext? context = null,
        CancellationToken ct = default)
    {
        // Step 1: Deterministic analysis (always runs)
        var deterministic = await _deterministicAnalyzer.AnalyzeAsync(input, context, ct);

        // Step 2: LLM analysis (optional, may fail)
        IntentAnalysisResult? llm = null;
        try
        {
            if (LlmIntentAnalyzerExtensions.IsAvailable(_llmAnalyzer))
            {
                llm = await _llmAnalyzer.AnalyzeAsync(input, context, ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM intent analysis failed; using deterministic only");
        }

        // Step 3: Resolve effective intent
        var resolved = _resolver.Resolve(deterministic, llm);

        _logger.LogDebug(
            "Intent resolved: {Intent} (deterministic={DetIntent}, llm={LlmIntent})",
            resolved.Intent, deterministic.Intent, llm?.Intent.ToString() ?? "N/A");

        return resolved;
    }
}

/// <summary>
/// Whether the LLM intent analyzer is available.
/// </summary>
internal static class LlmIntentAnalyzerExtensions
{
    public static bool IsAvailable(this LlmIntentAnalyzer analyzer)
    {
        // Check if the analyzer can make LLM calls
        return true; // The analyzer handles availability internally
    }
}
