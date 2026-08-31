using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DeveloperMemory.Domain.Configuration;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Orchestrates extraction from multiple strategies.
/// Combines deterministic and LLM extraction, deduplicates, and validates.
///
/// Pipeline:
///   Input
///     ↓
///   Deterministic extraction
///     ↓
///   LLM extraction (if available and enabled)
///     ↓
///   Candidate normalization
///     ↓
///   Deduplication
///     ↓
///   Policy validation
///     ↓
///   Final candidates
/// </summary>
public class ExtractionOrchestrator : IExtractionOrchestrator
{
    private readonly DeterministicExtractionStrategy _deterministicStrategy;
    private readonly LlmMemoryExtractionStrategy? _llmStrategy;
    private readonly IMemoryPolicy _policy;
    private readonly MemoryIntelligenceOptions _options;
    private readonly ILogger<ExtractionOrchestrator> _logger;

    public ExtractionOrchestrator(
        DeterministicExtractionStrategy deterministicStrategy,
        IMemoryPolicy policy,
        IOptions<MemoryIntelligenceOptions> options,
        ILogger<ExtractionOrchestrator> logger,
        LlmMemoryExtractionStrategy? llmStrategy = null)
    {
        _deterministicStrategy = deterministicStrategy;
        _llmStrategy = llmStrategy;
        _policy = policy;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ExtractionOrchestrationResult> ExtractAsync(
        MemoryExtractionRequest request,
        ExtractionMode mode = ExtractionMode.Auto,
        CancellationToken ct = default)
    {
        var result = new ExtractionOrchestrationResult();

        // ── Step 1: Deterministic extraction (always runs) ──
        var deterministicCandidates = await _deterministicStrategy.ExtractAsync(request, ct);
        result.DeterministicCount = deterministicCandidates.Count;

        var allCandidates = new List<MemoryCandidate>(deterministicCandidates);

        // ── Step 2: LLM extraction (if available and enabled) �─
        bool llmUsed = false;

        if (ShouldUseLLM(mode) && _llmStrategy != null)
        {
            try
            {
                var llmCandidates = await _llmStrategy.ExtractAsync(request, ct);
                llmUsed = llmCandidates.Count > 0;
                result.LlmCount = llmCandidates.Count;
                allCandidates.AddRange(llmCandidates);
            }
            catch (OperationCanceledException)
            {
                throw; // Always propagate cancellation
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LLM extraction failed, using deterministic only");
                result.Warnings.Add("LLM extraction failed: " + ex.Message);
            }
        }

        result.LlmUsed = llmUsed;
        result.StrategyUsed = llmUsed ? "deterministic+llm" : "deterministic";

        // ── Step 3: Deduplicate candidates ──
        var deduplicated = DeduplicateCandidates(allCandidates);

        // ── Step 4: Validate through policy ──
        var validated = new List<MemoryCandidate>();
        foreach (var candidate in deduplicated)
        {
            var decision = _policy.Evaluate(candidate);

            if (decision.ShouldPersist)
            {
                // Update candidate with policy-adjusted values
                candidate.Importance = decision.FinalImportance;
                candidate.Confidence = decision.FinalConfidence;
                validated.Add(candidate);
            }
            else if (decision.RequiresReview)
            {
                // Include review candidates with flag
                candidate.Importance = decision.FinalImportance;
                candidate.Confidence = decision.FinalConfidence;
                validated.Add(candidate);
                result.Warnings.Add($"Requires review: {decision.Reason}");
            }
            else
            {
                _logger.LogDebug("Candidate ignored by policy: {Reason}", decision.Reason);
            }
        }

        result.Candidates = validated;
        result.FinalCount = validated.Count;

        _logger.LogInformation(
            "Extraction complete: {Deterministic} deterministic + {Llm} LLM = {Final} final (mode={Mode})",
            result.DeterministicCount, result.LlmCount, result.FinalCount, result.StrategyUsed);

        return result;
    }

    private bool ShouldUseLLM(ExtractionMode mode)
    {
        return mode switch
        {
            ExtractionMode.LLM => _options.IsAvailable,
            ExtractionMode.Deterministic => false,
            ExtractionMode.Auto => _options.IsAvailable,
            _ => false
        };
    }

    private static List<MemoryCandidate> DeduplicateCandidates(List<MemoryCandidate> candidates)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<MemoryCandidate>();

        foreach (var candidate in candidates)
        {
            // Normalize content for deduplication
            var normalized = candidate.Content.Trim().ToLowerInvariant();

            if (seen.Add(normalized))
            {
                result.Add(candidate);
            }
        }

        return result;
    }
}
