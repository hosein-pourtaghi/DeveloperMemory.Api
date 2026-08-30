using System.Text.RegularExpressions;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Orchestrates consolidation of normalized memory candidates against existing
/// persistent memories. Handles:
///
///   1. Exact duplicate detection (identical content)
///   2. Normalized duplicate detection (same after normalization)
///   3. High-similarity detection (likely updated version)
///   4. Conflict detection (contradictory information)
///   5. Lifecycle-aware consolidation (supersession, not deletion)
///   6. Provenance preservation (Source field tracking)
///
/// This service sits above both the knowledge/profile sources and the
/// MemoryIngestionService. It uses the existing MemoryConflictDetector for
/// conflict detection and the existing IMemoryRepository for persistence.
///
/// Design decisions:
///   - No automatic merge of conflicting information (RequiresReview)
///   - High-confidence duplicates are silently ignored (DuplicateIgnored)
///   - High-confidence updated versions trigger supersession
///   - Provenance is preserved in the Source field (comma-separated for merged sources)
///   - Confidence threshold for auto-actions: 0.85
/// </summary>
public class DocumentConsolidationService : IDocumentConsolidationService
{
    private readonly IMemoryRepository _memoryRepository;
    private readonly IMemoryConflictDetector _conflictDetector;
    private readonly ILogger<DocumentConsolidationService> _logger;

    /// <summary>Confidence threshold above which auto-actions (supersede, ignore) are taken.</summary>
    private const double AutoActionThreshold = 0.85;

    /// <summary>Similarity threshold for considering two memories as "updated version".</summary>
    private const double UpdatedVersionThreshold = 0.85;

    /// <summary>Similarity threshold below which memories are considered unrelated.</summary>
    private const double UnrelatedThreshold = 0.2;

    public DocumentConsolidationService(
        IMemoryRepository memoryRepository,
        IMemoryConflictDetector conflictDetector,
        ILogger<DocumentConsolidationService> logger)
    {
        _memoryRepository = memoryRepository;
        _conflictDetector = conflictDetector;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ConsolidationResult> ConsolidateAsync(
        CanonicalMemoryCandidate candidate,
        string ownerId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(candidate.Content) || candidate.Content.Trim().Length < 3)
        {
            return new ConsolidationResult
            {
                Action = ConsolidationAction.Rejected,
                Candidate = candidate,
                Reason = "Content is empty or too short"
            };
        }

        // Step 1: Retrieve candidate memories from the repository
        var existingMemories = await GetCandidateMemories(candidate, ownerId, ct);

        // Step 2: Find the best match
        var match = FindMatch(candidate, existingMemories);

        // Step 3: Act on the match
        if (match.BestMatch == null)
        {
            // No match — create new memory
            return await CreateNewMemoryAsync(candidate, ownerId, ct);
        }

        // Step 4: Handle exact/normalized duplicates
        if (match.IsExactMatch || match.IsNormalizedMatch)
        {
            _logger.LogInformation(
                "Consolidation: duplicate detected — candidate '{Title}' matches existing memory {Id} ({MatchType})",
                candidate.Title, match.BestMatch.Id,
                match.IsExactMatch ? "exact" : "normalized");

            return new ConsolidationResult
            {
                Action = ConsolidationAction.DuplicateIgnored,
                Candidate = candidate,
                MatchedMemory = match.BestMatch,
                DuplicateDetected = true,
                Reason = match.Explanation
            };
        }

        // Step 5: Handle high-similarity (likely updated version)
        if (match.IsUpdatedVersion && match.Similarity >= AutoActionThreshold)
        {
            _logger.LogInformation(
                "Consolidation: updated version — candidate '{Title}' (sim={Similarity:F2}) supersedes memory {Id}",
                candidate.Title, match.Similarity, match.BestMatch.Id);

            return await SupersedeMemoryAsync(candidate, match.BestMatch, match, ownerId, ct);
        }

        // Step 6: Handle conflicts
        if (match.IsConflict)
        {
            var tempMemory = CreateTempMemoryEntry(candidate, ownerId);

            var conflicts = _conflictDetector.DetectConflicts(tempMemory, [match.BestMatch]);

            if (conflicts.Count > 0)
            {
                var primaryConflict = conflicts.OrderByDescending(c => c.Confidence).First();

                if (primaryConflict.ShouldSupersede && primaryConflict.Confidence >= AutoActionThreshold)
                {
                    _logger.LogInformation(
                        "Consolidation: conflict resolved — candidate '{CandidateTitle}' supersedes memory {Id}: {Reason}",
                        candidate.Title, match.BestMatch.Id, primaryConflict.Explanation);

                    return await SupersedeMemoryAsync(candidate, match.BestMatch, match, ownerId, ct);
                }

                _logger.LogWarning(
                    "Consolidation: potential conflict — candidate '{CandidateTitle}' vs memory {Id}: {Reason} (confidence={Confidence:F2})",
                    candidate.Title, match.BestMatch.Id, primaryConflict.Explanation, primaryConflict.Confidence);

                return new ConsolidationResult
                {
                    Action = ConsolidationAction.RequiresReview,
                    Candidate = candidate,
                    MatchedMemory = match.BestMatch,
                    ConflictResolved = false,
                    Reason = $"Potential conflict: {primaryConflict.Explanation} (confidence: {primaryConflict.Confidence:F2})"
                };
            }
        }

        // Step 7: Moderate similarity — likely related but not identical. Create as new with provenance link.
        if (match.Similarity > UnrelatedThreshold)
        {
            _logger.LogInformation(
                "Consolidation: related but not duplicate — candidate '{Title}' (sim={Similarity:F2}) created as new alongside memory {Id}",
                candidate.Title, match.Similarity, match.BestMatch.Id);

            // Enhance source with provenance link
            var enrichedCandidate = EnrichSourceWithProvenance(candidate, match.BestMatch);
            return await CreateNewMemoryAsync(enrichedCandidate, ownerId, ct);
        }

        // No significant match — create new
        return await CreateNewMemoryAsync(candidate, ownerId, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ConsolidationResult>> ConsolidateBatchAsync(
        IReadOnlyList<CanonicalMemoryCandidate> candidates,
        string ownerId,
        CancellationToken ct = default)
    {
        var results = new List<ConsolidationResult>();

        foreach (var candidate in candidates)
        {
            try
            {
                var result = await ConsolidateAsync(candidate, ownerId, ct);
                results.Add(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Consolidation failed for candidate '{Title}': {Error}",
                    candidate.Title, ex.Message);

                results.Add(new ConsolidationResult
                {
                    Action = ConsolidationAction.Rejected,
                    Candidate = candidate,
                    Reason = $"Consolidation failed: {ex.Message}"
                });
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public ConsolidationMatch FindMatch(
        CanonicalMemoryCandidate candidate,
        IReadOnlyList<MemoryEntry> existingMemories)
    {
        var bestMatch = new ConsolidationMatch();

        foreach (var existing in existingMemories)
        {
            // Only compare with active or updated memories (not deleted/superseded/expired)
            if (!existing.IsActive && existing.State != MemoryState.Updated)
                continue;

            // Only compare within the same scope
            if (existing.Scope != candidate.Scope)
                continue;

            // Exact content match
            if (string.Equals(
                existing.Content.Trim(),
                candidate.Content.Trim(),
                StringComparison.OrdinalIgnoreCase))
            {
                return new ConsolidationMatch
                {
                    BestMatch = existing,
                    Similarity = 1.0,
                    IsExactMatch = true,
                    Explanation = "Identical content"
                };
            }

            // Normalized content match
            var existingNormalized = existing.NormalizedContent ?? ComputeNormalizedContent(existing.Content);
            if (string.Equals(existingNormalized, candidate.NormalizedContent, StringComparison.OrdinalIgnoreCase))
            {
                return new ConsolidationMatch
                {
                    BestMatch = existing,
                    Similarity = 0.95,
                    IsNormalizedMatch = true,
                    Explanation = "Same content after normalization"
                };
            }

            // Compute similarity
            var similarity = ComputeSimilarity(candidate.NormalizedContent, existingNormalized);

            // Check for contradiction (same memory type)
            if (candidate.MemoryType == existing.MemoryType && DetectContradiction(candidate.Content, existing.Content))
            {
                if (similarity > bestMatch.Similarity)
                {
                    bestMatch = new ConsolidationMatch
                    {
                        BestMatch = existing,
                        Similarity = similarity,
                        IsConflict = true,
                        Explanation = $"Conflicting statements detected (similarity: {similarity:P0})"
                    };
                }
            }
            // High similarity (updated version)
            else if (similarity >= UpdatedVersionThreshold)
            {
                if (similarity > bestMatch.Similarity)
                {
                    bestMatch = new ConsolidationMatch
                    {
                        BestMatch = existing,
                        Similarity = similarity,
                        IsUpdatedVersion = true,
                        Explanation = $"High content similarity ({similarity:P0})"
                    };
                }
            }
            // Moderate similarity — still track as potential match
            else if (similarity > bestMatch.Similarity && similarity > UnrelatedThreshold)
            {
                bestMatch = new ConsolidationMatch
                {
                    BestMatch = existing,
                    Similarity = similarity,
                    Explanation = $"Related content (similarity: {similarity:P0})"
                };
            }
        }

        return bestMatch;
    }

    // ── Private helpers ──

    private async Task<List<MemoryEntry>> GetCandidateMemories(
        CanonicalMemoryCandidate candidate, string ownerId, CancellationToken ct)
    {
        // Search for candidates in the same scope
        var memories = await _memoryRepository.SearchAsync(
            candidate.Content,
            ownerId,
            scope: candidate.Scope,
            projectId: candidate.ProjectId,
            ct: ct);

        // Also fetch project-scoped memories if candidate is project-scoped
        if (candidate.ProjectId.HasValue)
        {
            var projectMemories = await _memoryRepository.GetByScopeAsync(
                MemoryScope.Project,
                ownerId,
                candidate.ProjectId,
                ct);

            // Merge, avoiding duplicates
            foreach (var m in projectMemories)
            {
                if (!memories.Any(e => e.Id == m.Id))
                    memories.Add(m);
            }
        }

        return memories;
    }

    private async Task<ConsolidationResult> CreateNewMemoryAsync(
        CanonicalMemoryCandidate candidate, string ownerId, CancellationToken ct)
    {
        var entry = new MemoryEntry
        {
            Title = candidate.Title,
            Content = candidate.Content,
            NormalizedContent = candidate.NormalizedContent,
            Scope = candidate.Scope,
            State = MemoryState.Active,
            MemoryType = candidate.MemoryType,
            Classification = candidate.Classification,
            ProjectId = candidate.Scope == MemoryScope.Project ? candidate.ProjectId : null,
            WorkspaceId = candidate.Scope == MemoryScope.Workspace ? candidate.WorkspaceId : null,
            OwnerId = ownerId,
            Source = candidate.Source,
            Importance = candidate.Importance,
            Confidence = candidate.Confidence,
            ExpiresAt = candidate.ExpiresAt,
            MetadataJson = candidate.MetadataJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (candidate.Tags.Count > 0)
        {
            entry.SetTags(candidate.Tags);
        }

        var created = await _memoryRepository.CreateAsync(entry, ct);

        _logger.LogInformation(
            "Consolidation: created memory {Id} from '{Source}' (type={Type}, scope={Scope})",
            created.Id, candidate.Source, candidate.MemoryType, candidate.Scope);

        return new ConsolidationResult
        {
            Action = ConsolidationAction.Created,
            Memory = created,
            Candidate = candidate,
            Reason = "New memory created",
            ProvenancePreserved = true
        };
    }

    private async Task<ConsolidationResult> SupersedeMemoryAsync(
        CanonicalMemoryCandidate candidate,
        MemoryEntry existing,
        ConsolidationMatch match,
        string ownerId,
        CancellationToken ct)
    {
        // Create the new memory with a reference to the old one
        var entry = new MemoryEntry
        {
            Title = candidate.Title,
            Content = candidate.Content,
            NormalizedContent = candidate.NormalizedContent,
            Scope = candidate.Scope,
            State = MemoryState.Active,
            MemoryType = candidate.MemoryType,
            Classification = candidate.Classification,
            ProjectId = candidate.Scope == MemoryScope.Project ? candidate.ProjectId : null,
            WorkspaceId = candidate.Scope == MemoryScope.Workspace ? candidate.WorkspaceId : null,
            OwnerId = ownerId,
            Source = MergeProvenance(candidate.Source, existing.Source),
            Importance = Math.Max(candidate.Importance, existing.Importance),
            Confidence = candidate.Confidence,
            SupersedesId = existing.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (candidate.Tags.Count > 0)
        {
            entry.SetTags(candidate.Tags);
        }

        var created = await _memoryRepository.CreateAsync(entry, ct);

        // Mark the old memory as superseded
        existing.Supersede(created.Id);
        await _memoryRepository.UpdateAsync(existing, ct);

        _logger.LogInformation(
            "Consolidation: memory {NewId} supersedes {OldId} (from '{Source}'): {Reason}",
            created.Id, existing.Id, candidate.Source, match.Explanation);

        return new ConsolidationResult
        {
            Action = ConsolidationAction.SupersededExisting,
            Memory = created,
            MatchedMemory = existing,
            Candidate = candidate,
            Reason = $"Superseded existing memory: {match.Explanation}",
            ConflictResolved = match.IsConflict,
            ProvenancePreserved = true
        };
    }

    private static MemoryEntry CreateTempMemoryEntry(CanonicalMemoryCandidate candidate, string ownerId)
    {
        return new MemoryEntry
        {
            Title = candidate.Title,
            Content = candidate.Content,
            NormalizedContent = candidate.NormalizedContent,
            Scope = candidate.Scope,
            MemoryType = candidate.MemoryType,
            ProjectId = candidate.ProjectId,
            OwnerId = ownerId,
            Source = candidate.Source
        };
    }

    private static CanonicalMemoryCandidate EnrichSourceWithProvenance(
        CanonicalMemoryCandidate candidate, MemoryEntry existing)
    {
        var enriched = new CanonicalMemoryCandidate
        {
            Title = candidate.Title,
            Content = candidate.Content,
            NormalizedContent = candidate.NormalizedContent,
            MemoryType = candidate.MemoryType,
            Scope = candidate.Scope,
            Classification = candidate.Classification,
            ProjectId = candidate.ProjectId,
            Source = MergeProvenance(candidate.Source, existing.Source),
            Tags = candidate.Tags.Distinct().ToList(),
            Confidence = candidate.Confidence,
            Importance = candidate.Importance,
            ExpiresAt = candidate.ExpiresAt,
            MetadataJson = candidate.MetadataJson
        };

        return enriched;
    }

    /// <summary>
    /// Merges provenance information from two sources.
    /// Produces a comma-separated list of unique sources.
    /// </summary>
    private static string MergeProvenance(string? sourceA, string? sourceB)
    {
        var sources = new List<string>();

        if (!string.IsNullOrWhiteSpace(sourceA))
            sources.Add(sourceA.Trim());

        if (!string.IsNullOrWhiteSpace(sourceB))
        {
            var b = sourceB.Trim();
            if (!sources.Any(s => string.Equals(s, b, StringComparison.OrdinalIgnoreCase)))
                sources.Add(b);
        }

        return sources.Count > 0 ? string.Join(", ", sources) : string.Empty;
    }

    private static string ComputeNormalizedContent(string content)
    {
        var text = content.ToLowerInvariant();
        text = Regex.Replace(text, @"[^\w\s]", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    private static double ComputeSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        var wordsA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (wordsA.Length == 0 || wordsB.Length == 0) return 0.0;

        var setA = new HashSet<string>(wordsA);
        var setB = new HashSet<string>(wordsB);

        var intersection = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    private static bool DetectContradiction(string newContent, string existingContent)
    {
        var newLower = newContent.ToLowerInvariant();
        var existingLower = existingContent.ToLowerInvariant();

        var negations = new[] { "don't use ", "do not use ", "avoid ", "no ", "never ", "not " };

        foreach (var negation in negations)
        {
            if (newLower.Contains(negation) && !existingLower.Contains(negation))
            {
                var stripped = newLower.Replace(negation, "").Trim();
                if (ComputeSimilarity(stripped, existingLower) > 0.7)
                    return true;
            }

            if (existingLower.Contains(negation) && !newLower.Contains(negation))
            {
                var stripped = existingLower.Replace(negation, "").Trim();
                if (ComputeSimilarity(newLower, stripped) > 0.7)
                    return true;
            }
        }

        return false;
    }
}
