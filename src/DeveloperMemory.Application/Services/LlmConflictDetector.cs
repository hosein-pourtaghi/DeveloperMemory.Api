using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DeveloperMemory.Domain.Configuration;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// LLM-assisted conflict detection.
/// Extends the deterministic conflict detector with semantic understanding.
///
/// Only called when:
/// 1. Deterministic detector returns no high-confidence result
/// 2. LLM is available
/// 3. Within rate limits
///
/// The LLM suggests; the application decides.
/// </summary>
public class LlmConflictDetector : IMemoryConflictDetector
{
    private readonly IMemoryConflictDetector _deterministicDetector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MemoryIntelligenceOptions _options;
    private readonly ILogger<LlmConflictDetector> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public LlmConflictDetector(
        IMemoryConflictDetector deterministicDetector,
        IHttpClientFactory httpClientFactory,
        IOptions<MemoryIntelligenceOptions> options,
        ILogger<LlmConflictDetector> logger)
    {
        _deterministicDetector = deterministicDetector;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyList<MemoryConflict> DetectConflicts(
        MemoryEntry candidate,
        IReadOnlyList<MemoryEntry> existingMemories)
    {
        // Always run deterministic detection first
        var deterministicConflicts = _deterministicDetector.DetectConflicts(candidate, existingMemories);

        // If deterministic found a high-confidence conflict, use it
        if (deterministicConflicts.Any(c => c.Confidence >= 0.8))
        {
            return deterministicConflicts;
        }

        // If LLM is not available or not enabled, return deterministic results
        if (!_options.IsAvailable || existingMemories.Count == 0)
        {
            return deterministicConflicts;
        }

        // Check rate limits
        if (deterministicConflicts.Count >= _options.MaxConflictChecks)
        {
            return deterministicConflicts;
        }

        // For now, return deterministic results
        // LLM conflict detection would be async and is deferred to a future enhancement
        return deterministicConflicts;
    }

    /// <summary>
    /// Async version for future LLM-enhanced conflict detection.
    /// Currently returns deterministic results only.
    /// </summary>
    public async Task<IReadOnlyList<MemoryConflict>> DetectConflictsAsync(
        MemoryEntry candidate,
        IReadOnlyList<MemoryEntry> existingMemories,
        CancellationToken ct = default)
    {
        // Always run deterministic detection first
        var deterministicConflicts = _deterministicDetector.DetectConflicts(candidate, existingMemories);

        // If deterministic found a high-confidence conflict, use it
        if (deterministicConflicts.Any(c => c.Confidence >= 0.8))
        {
            return deterministicConflicts;
        }

        // If LLM is not available, return deterministic results
        if (!_options.IsAvailable || existingMemories.Count == 0)
        {
            return deterministicConflicts;
        }

        try
        {
            // For efficiency, only check the top N most similar memories
            var topMemories = existingMemories
                .OrderByDescending(m => CalculateContentSimilarity(candidate.Content, m.Content))
                .Take(_options.MaxConflictChecks)
                .ToList();

            var llmConflicts = await DetectConflictsViaLLM(candidate, topMemories, ct);

            // Merge results — deterministic takes precedence for high confidence
            var allConflicts = new List<MemoryConflict>(deterministicConflicts);
            allConflicts.AddRange(llmConflicts);

            return allConflicts
                .GroupBy(c => c.ExistingMemory.Id)
                .Select(g => g.OrderByDescending(c => c.Confidence).First())
                .ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM conflict detection failed, using deterministic only");
            return deterministicConflicts;
        }
    }

    private async Task<List<MemoryConflict>> DetectConflictsViaLLM(
        MemoryEntry candidate,
        IReadOnlyList<MemoryEntry> existingMemories,
        CancellationToken ct)
    {
        var prompt = BuildConflictPrompt(candidate, existingMemories);

        var client = _httpClientFactory.CreateClient("MemoryExtraction");
        var endpoint = $"{_options.ConflictProvider}/chat/completions";

        var request = new
        {
            model = _options.ConflictModel,
            messages = new[]
            {
                new { role = "system", content = ConflictSystemPrompt },
                new { role = "user", content = prompt }
            },
            temperature = 0.1,
            max_tokens = 1000,
            response_format = new { type = "json_object" }
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.ConflictTimeoutSeconds));

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(endpoint, content, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        return ParseConflictResponse(responseJson, existingMemories);
    }

    private static string BuildConflictPrompt(
        MemoryEntry candidate,
        IReadOnlyList<MemoryEntry> existingMemories)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze whether the NEW memory conflicts with any EXISTING memories.");
        sb.AppendLine();
        sb.AppendLine("NEW MEMORY:");
        sb.AppendLine($"Content: {candidate.Content}");
        sb.AppendLine($"Type: {candidate.MemoryType}");
        sb.AppendLine();
        sb.AppendLine("EXISTING MEMORIES:");

        foreach (var memory in existingMemories)
        {
            sb.AppendLine($"- [{memory.Id}] ({memory.MemoryType}): {TruncateContent(memory.Content, 200)}");
        }

        sb.AppendLine();
        sb.AppendLine("Return JSON:");
        sb.AppendLine("""
        {
          "conflicts": [
            {
              "existing_id": "guid",
              "type": "NoConflict|Duplicate|Update|Contradiction|PotentialConflict",
              "confidence": 0.9,
              "explanation": "Brief explanation"
            }
          ]
        }
        """);

        return sb.ToString();
    }

    private const string ConflictSystemPrompt = """
        You are a memory conflict detection assistant. Analyze whether new memories conflict with existing ones.

        CONFLICT TYPES:
        - NoConflict: No relationship between memories
        - Duplicate: Same information expressed differently
        - Update: New memory is a newer version of existing
        - Contradiction: Memories contain conflicting information
        - PotentialConflict: Possible overlap, needs review

        RULES:
        1. Be conservative — only flag clear conflicts
        2. Consider memory type when judging conflicts
        3. An update supersedes the older memory
        4. A contradiction means both cannot be true
        5. Return structured JSON only
        """;

    private List<MemoryConflict> ParseConflictResponse(
        string responseJson,
        IReadOnlyList<MemoryEntry> existingMemories)
    {
        try
        {
            var response = JsonSerializer.Deserialize<ConflictResponse>(responseJson, JsonOptions);
            if (response?.Conflicts == null)
            {
                return [];
            }

            var conflicts = new List<MemoryConflict>();

            foreach (var conflict in response.Conflicts)
            {
                if (conflict.Type == "NoConflict")
                {
                    continue;
                }

                // Find the existing memory
                var existingId = Guid.Parse(conflict.ExistingId);
                var existing = existingMemories.FirstOrDefault(m => m.Id == existingId);
                if (existing == null)
                {
                    continue;
                }

                var conflictType = conflict.Type switch
                {
                    "Duplicate" => MemoryConflictType.ExactDuplicate,
                    "Update" => MemoryConflictType.SemanticUpdate,
                    "Contradiction" => MemoryConflictType.Contradiction,
                    "PotentialConflict" => MemoryConflictType.PotentialConflict,
                    _ => MemoryConflictType.PotentialConflict
                };

                conflicts.Add(new MemoryConflict
                {
                    ConflictType = conflictType,
                    ExistingMemory = existing,
                    Confidence = Math.Clamp(conflict.Confidence, 0.0, 1.0),
                    Explanation = conflict.Explanation ?? "LLM conflict detection",
                    ShouldSupersede = conflict.Type == "Update" && conflict.Confidence >= 0.7
                });
            }

            return conflicts;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse conflict detection response");
            return [];
        }
    }

    private static double CalculateContentSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return 0.0;
        }

        var wordsA = a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordsB = b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var setA = new HashSet<string>(wordsA);
        var setB = new HashSet<string>(wordsB);

        var intersection = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    private static string TruncateContent(string content, int maxLength)
    {
        if (content.Length <= maxLength) return content;
        return content[..maxLength] + "...";
    }
}

// ── LLM conflict response DTOs ──

internal class ConflictResponse
{
    [JsonPropertyName("conflicts")]
    public List<ConflictResult> Conflicts { get; set; } = [];
}

internal class ConflictResult
{
    [JsonPropertyName("existing_id")]
    public string ExistingId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "NoConflict";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("explanation")]
    public string? Explanation { get; set; }
}
