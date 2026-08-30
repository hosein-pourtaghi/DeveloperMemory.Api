using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DeveloperMemory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KnowledgeController : ControllerBase
{
    private readonly KnowledgeService _knowledgeService;
    private readonly IMemoryNormalizationService _normalizer;
    private readonly IDocumentConsolidationService _consolidationService;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<KnowledgeController> _logger;

    public KnowledgeController(
        KnowledgeService knowledgeService,
        IMemoryNormalizationService normalizer,
        IDocumentConsolidationService consolidationService,
        ICurrentUser currentUser,
        ILogger<KnowledgeController> logger)
    {
        _knowledgeService = knowledgeService;
        _normalizer = normalizer;
        _consolidationService = consolidationService;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<List<SearchResult>> SearchDocuments([FromQuery] string query, [FromQuery] string? project, [FromQuery] List<string>? tags)
    {
        var results = _knowledgeService.SearchDocuments(query, project, tags);
        return Ok(results);
    }

    [HttpGet("documents")]
    public async Task<ActionResult<List<KnowledgeDocument>>> GetDocuments()
    {
        var documents = await _knowledgeService.LoadDocumentsAsync();
        return Ok(documents);
    }

    [HttpPost("reindex")]
    public async Task<ActionResult<List<KnowledgeDocument>>> Reindex()
    {
        var documents = await _knowledgeService.ReindexDocumentsAsync();
        return Ok(documents);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<KnowledgeDocument>> GetDocument(Guid id)
    {
        var documents = await _knowledgeService.LoadDocumentsAsync();
        var document = documents.FirstOrDefault(d => d.Id == id);
        
        if (document == null)
        {
            return NotFound();
        }
        
        return Ok(document);
    }

    [HttpPost]
    public async Task<ActionResult<KnowledgeDocument>> CreateDocument([FromBody] CreateDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("Title and content are required");
        }

        var document = await _knowledgeService.CreateDocumentAsync(
            request.Title,
            request.Content,
            request.Project,
            request.Tags);

        return Ok(document);
    }

    /// <summary>
    /// Consolidate all knowledge documents into persistent memory.
    /// Normalizes each document, detects duplicates against existing memories,
    /// and creates or supersedes memories as appropriate.
    /// 
    /// This is the primary ingestion path for knowledge→memory consolidation.
    /// Existing knowledge documents remain in place; this creates a canonical
    /// memory representation of their content.
    /// </summary>
    [HttpPost("consolidate")]
    public async Task<ActionResult<ConsolidationResponse>> Consolidate([FromBody] ConsolidationRequest? request = null, CancellationToken ct = default)
    {
        var documents = await _knowledgeService.LoadDocumentsAsync();
        if (documents.Count == 0)
        {
            return Ok(new ConsolidationResponse
            {
                DocumentsProcessed = 0,
                MemoriesCreated = 0,
                DuplicatesIgnored = 0,
                ConflictsResolved = 0,
                RequiresReview = 0
            });
        }

        var candidates = new List<Application.Contracts.CanonicalMemoryCandidate>();
        var projectId = request?.ProjectId;

        foreach (var doc in documents)
        {
            var docCandidates = _normalizer.NormalizeKnowledgeDocument(
                doc.Title,
                doc.Content,
                project: doc.Project,
                tags: doc.Tags,
                filePath: doc.FilePath);

            // If a project ID was specified, override scope
            if (projectId.HasValue)
            {
                foreach (var c in docCandidates)
                {
                    c.ProjectId = projectId.Value;
                    c.Scope = MemoryScope.Project;
                }
            }

            candidates.AddRange(docCandidates);
        }

        var results = await _consolidationService.ConsolidateBatchAsync(
            candidates, _currentUser.UserId, ct);

        var response = new ConsolidationResponse
        {
            DocumentsProcessed = documents.Count,
            CandidatesGenerated = candidates.Count,
            MemoriesCreated = results.Count(r => r.Action == ConsolidationAction.Created),
            DuplicatesIgnored = results.Count(r => r.Action == ConsolidationAction.DuplicateIgnored),
            ConflictsResolved = results.Count(r => r.Action == ConsolidationAction.SupersededExisting),
            RequiresReview = results.Count(r => r.Action == ConsolidationAction.RequiresReview),
            Results = results.Select(r => new ConsolidationResultSummary
            {
                Action = r.Action.ToString(),
                Title = r.Candidate?.Title ?? string.Empty,
                Source = r.Candidate?.Source ?? string.Empty,
                MemoryId = r.Memory?.Id,
                MatchedMemoryId = r.MatchedMemory?.Id,
                Reason = r.Reason,
                DuplicateDetected = r.DuplicateDetected,
                ConflictResolved = r.ConflictResolved
            }).ToList()
        };

        _logger.LogInformation(
            "Knowledge consolidation complete: {Docs} docs → {Candidates} candidates, " +
            "{Created} created, {Dupes} duplicates, {Superseded} superseded, {Review} review",
            response.DocumentsProcessed, response.CandidatesGenerated,
            response.MemoriesCreated, response.DuplicatesIgnored,
            response.ConflictsResolved, response.RequiresReview);

        return Ok(response);
    }
}

public class ConsolidationRequest
{
    /// <summary>Optional project ID to associate all consolidated memories with.</summary>
    public Guid? ProjectId { get; set; }
}

public class ConsolidationResponse
{
    public int DocumentsProcessed { get; set; }
    public int CandidatesGenerated { get; set; }
    public int MemoriesCreated { get; set; }
    public int DuplicatesIgnored { get; set; }
    public int ConflictsResolved { get; set; }
    public int RequiresReview { get; set; }
    public List<ConsolidationResultSummary> Results { get; set; } = [];
}

public class ConsolidationResultSummary
{
    public string Action { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public Guid? MemoryId { get; set; }
    public Guid? MatchedMemoryId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool DuplicateDetected { get; set; }
    public bool ConflictResolved { get; set; }
}