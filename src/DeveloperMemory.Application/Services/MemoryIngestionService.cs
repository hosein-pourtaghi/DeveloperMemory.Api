using System.Text.RegularExpressions;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Deterministic memory ingestion service.
/// Handles normalization, duplicate detection, conflict detection,
/// and lifecycle decisions for new memory.
/// </summary>
public class MemoryIngestionService : IMemoryIngestionService
{
    private readonly IMemoryRepository _memoryRepository;
    private readonly IMemoryConflictDetector _conflictDetector;
    private readonly ILogger<MemoryIngestionService> _logger;

    public MemoryIngestionService(
        IMemoryRepository memoryRepository,
        IMemoryConflictDetector conflictDetector,
        ILogger<MemoryIngestionService> logger)
    {
        _memoryRepository = memoryRepository;
        _conflictDetector = conflictDetector;
        _logger = logger;
    }

    public async Task<MemoryIngestionResult> IngestAsync(
        MemoryIngestionRequest request,
        CancellationToken ct = default)
    {
        // ── Step 1: Validate input ──
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return new MemoryIngestionResult
            {
                Outcome = MemoryIngestionOutcome.Rejected,
                Reason = "Content is required"
            };
        }

        if (request.Content.Length > 10000)
        {
            return new MemoryIngestionResult
            {
                Outcome = MemoryIngestionOutcome.Rejected,
                Reason = $"Content exceeds maximum length of 10000 characters (was {request.Content.Length})"
            };
        }

        // ── Step 2: Normalize content ──
        var normalizedContent = NormalizeContent(request.Content);

        // ── Step 3: Check for exact duplicates ──
        var existingMemories = await GetCandidateMemories(request, ct);

        var exactDuplicate = existingMemories.FirstOrDefault(e =>
            string.Equals(e.Content.Trim(), request.Content.Trim(), StringComparison.OrdinalIgnoreCase));

        if (exactDuplicate != null)
        {
            _logger.LogInformation(
                "Duplicate ignored: memory {Id} matches new content exactly", exactDuplicate.Id);
            return new MemoryIngestionResult
            {
                Outcome = MemoryIngestionOutcome.IgnoredDuplicate,
                Memory = exactDuplicate,
                RelatedMemory = exactDuplicate,
                Reason = "Exact duplicate content found",
                DuplicateDetected = true
            };
        }

        // ── Step 4: Check for normalized duplicates ──
        var normalizedDuplicate = existingMemories.FirstOrDefault(e =>
        {
            var existingNormalized = e.NormalizedContent ?? NormalizeContent(e.Content);
            return string.Equals(existingNormalized, normalizedContent, StringComparison.OrdinalIgnoreCase);
        });

        if (normalizedDuplicate != null)
        {
            _logger.LogInformation(
                "Normalized duplicate ignored: memory {Id} matches normalized content", normalizedDuplicate.Id);
            return new MemoryIngestionResult
            {
                Outcome = MemoryIngestionOutcome.IgnoredDuplicate,
                Memory = normalizedDuplicate,
                RelatedMemory = normalizedDuplicate,
                Reason = "Normalized duplicate content found",
                DuplicateDetected = true
            };
        }

        // ── Step 5: Detect conflicts ──
        var conflicts = _conflictDetector.DetectConflicts(
            CreateTempMemory(request, normalizedContent),
            existingMemories);

        if (conflicts.Count > 0)
        {
            var primaryConflict = conflicts.OrderByDescending(c => c.Confidence).First();

            if (primaryConflict.ShouldSupersede && primaryConflict.Confidence >= 0.8)
            {
                // High-confidence conflict with supersession recommendation
                var newEntry = CreateMemoryEntry(request, normalizedContent);
                newEntry.SupersedesId = primaryConflict.ExistingMemory.Id;
                var created = await _memoryRepository.CreateAsync(newEntry, ct);

                // Supersede the old memory
                primaryConflict.ExistingMemory.Supersede(created.Id);
                await _memoryRepository.UpdateAsync(primaryConflict.ExistingMemory, ct);

                _logger.LogInformation(
                    "Memory {NewId} supersedes {OldId}: {Reason}",
                    created.Id, primaryConflict.ExistingMemory.Id, primaryConflict.Explanation);

                return new MemoryIngestionResult
                {
                    Outcome = MemoryIngestionOutcome.SupersededExisting,
                    Memory = created,
                    RelatedMemory = primaryConflict.ExistingMemory,
                    Reason = primaryConflict.Explanation,
                    ConflictDetected = true,
                    WasPersisted = true
                };
            }

            if (primaryConflict.ConflictType == MemoryConflictType.NormalizedDuplicate ||
                primaryConflict.ConflictType == MemoryConflictType.ExactDuplicate)
            {
                return new MemoryIngestionResult
                {
                    Outcome = MemoryIngestionOutcome.IgnoredDuplicate,
                    RelatedMemory = primaryConflict.ExistingMemory,
                    Reason = primaryConflict.Explanation,
                    DuplicateDetected = true
                };
            }

            // Potential conflict — require review
            _logger.LogWarning(
                "Potential conflict detected: {Type} — {Explanation}",
                primaryConflict.ConflictType, primaryConflict.Explanation);

            return new MemoryIngestionResult
            {
                Outcome = MemoryIngestionOutcome.RequiresReview,
                RelatedMemory = primaryConflict.ExistingMemory,
                Reason = $"Potential conflict: {primaryConflict.Explanation}",
                ConflictDetected = true
            };
        }

        // ── Step 6: Create new memory ──
        var entry = CreateMemoryEntry(request, normalizedContent);
        var persisted = await _memoryRepository.CreateAsync(entry, ct);

        _logger.LogInformation(
            "Memory created: {Id} (type={Type}, scope={Scope})",
            persisted.Id, request.MemoryType, request.Scope);

        return new MemoryIngestionResult
        {
            Outcome = MemoryIngestionOutcome.Created,
            Memory = persisted,
            Reason = "New memory created",
            WasPersisted = true
        };
    }

    private async Task<List<MemoryEntry>> GetCandidateMemories(
        MemoryIngestionRequest request, CancellationToken ct)
    {
        // Get memories in the same scope for comparison
        return request.Scope switch
        {
            MemoryScope.Project when request.ProjectId.HasValue =>
                await _memoryRepository.GetByScopeAsync(MemoryScope.Project, request.ProjectId, ct),
            MemoryScope.Workspace when !string.IsNullOrEmpty(request.WorkspaceId) =>
                await _memoryRepository.SearchAsync(request.Content, MemoryScope.Workspace, ct: ct),
            MemoryScope.Private when !string.IsNullOrEmpty(request.UserId) =>
                await _memoryRepository.SearchAsync(request.Content, MemoryScope.Private, ct: ct),
            _ => await _memoryRepository.SearchAsync(request.Content, MemoryScope.Global, ct: ct)
        };
    }

    private static string NormalizeContent(string content)
    {
        var text = content.ToLowerInvariant();
        text = Regex.Replace(text, @"[^\w\s]", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    private static MemoryEntry CreateTempMemory(MemoryIngestionRequest request, string normalizedContent)
    {
        return new MemoryEntry
        {
            Title = request.Title,
            Content = request.Content,
            NormalizedContent = normalizedContent,
            Scope = request.Scope,
            MemoryType = request.MemoryType,
            ProjectId = request.ProjectId,
            WorkspaceId = request.WorkspaceId,
            UserId = request.UserId,
            TagsJson = request.Tags != null
                ? System.Text.Json.JsonSerializer.Serialize(request.Tags)
                : null
        };
    }

    private static MemoryEntry CreateMemoryEntry(MemoryIngestionRequest request, string normalizedContent)
    {
        return new MemoryEntry
        {
            Title = request.Title,
            Content = request.Content,
            NormalizedContent = normalizedContent,
            Scope = request.Scope,
            State = MemoryState.Active,
            MemoryType = request.MemoryType,
            Classification = request.Classification,
            ProjectId = request.Scope == MemoryScope.Project ? request.ProjectId : null,
            WorkspaceId = request.Scope == MemoryScope.Workspace ? request.WorkspaceId : null,
            UserId = request.Scope == MemoryScope.Private ? request.UserId : null,
            Source = request.Source,
            Importance = request.Importance,
            Confidence = request.Confidence,
            ExpiresAt = request.ExpiresAt,
            MetadataJson = request.MetadataJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TagsJson = request.Tags != null
                ? System.Text.Json.JsonSerializer.Serialize(request.Tags)
                : null
        };
    }
}
