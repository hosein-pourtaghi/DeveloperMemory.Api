using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// MemoryNormalizer tests — normalization, memory type inference, provenance
// ══════════════════════════════════════════════════════════════════════════════

public class MemoryNormalizerTests
{
    private readonly MemoryNormalizer _normalizer = new();

    // ── Knowledge Document Normalization ──

    [Fact]
    public void NormalizeKnowledgeDocument_SimpleContent_ReturnsSingleCandidate()
    {
        var result = _normalizer.NormalizeKnowledgeDocument(
            "Architecture",
            "We use Clean Architecture with 4 projects.",
            project: "DeveloperMemory");

        Assert.Single(result);
        Assert.Equal("Architecture", result[0].Title);
        Assert.Contains("Clean Architecture", result[0].Content);
        Assert.Equal(0.9, result[0].Confidence);
        Assert.Equal("knowledge:Architecture", result[0].Source);
    }

    [Fact]
    public void NormalizeKnowledgeDocument_EmptyContent_ReturnsEmpty()
    {
        var result = _normalizer.NormalizeKnowledgeDocument("Empty", "");
        Assert.Empty(result);
    }

    [Fact]
    public void NormalizeKnowledgeDocument_WhitespaceOnly_ReturnsEmpty()
    {
        var result = _normalizer.NormalizeKnowledgeDocument("Whitespace", "   \n  \n  ");
        Assert.Empty(result);
    }

    [Fact]
    public void NormalizeKnowledgeDocument_MultipleSections_CreatesMultipleCandidates()
    {
        var content = @"## Database
We use PostgreSQL for persistence.

## Frontend
We use Blazor for the UI.

## Testing
We use xUnit for unit tests.";

        var result = _normalizer.NormalizeKnowledgeDocument(
            "Tech Stack",
            content,
            project: "MyProject");

        Assert.Equal(3, result.Count);
        Assert.Contains(result, c => c.Title == "Database");
        Assert.Contains(result, c => c.Title == "Frontend");
        Assert.Contains(result, c => c.Title == "Testing");
    }

    [Fact]
    public void NormalizeKnowledgeDocument_ProjectInferred()
    {
        var result = _normalizer.NormalizeKnowledgeDocument(
            "DB Choice",
            "Use PostgreSQL for all databases",
            project: "MyApp");

        Assert.Single(result);
        Assert.Equal(MemoryScope.Project, result[0].Scope);
    }

    [Fact]
    public void NormalizeKnowledgeDocument_TagsInferred()
    {
        var result = _normalizer.NormalizeKnowledgeDocument(
            "DB Tech",
            "We use PostgreSQL and Redis for caching");

        Assert.Single(result);
        Assert.Contains("database", result[0].Tags);
    }

    [Fact]
    public void NormalizeKnowledgeDocument_FilePathUsedAsSource()
    {
        var result = _normalizer.NormalizeKnowledgeDocument(
            "Doc",
            "Some content about APIs and endpoints",
            filePath: "/knowledge/api-guide.md");

        Assert.Equal("knowledge:api-guide", result[0].Source);
    }

    [Fact]
    public void NormalizeKnowledgeDocument_NormalizedContentIsLoweredStripped()
    {
        var result = _normalizer.NormalizeKnowledgeDocument(
            "Title",
            "Use PostgreSQL for the database!");

        Assert.Equal("title use postgresql for the database", result[0].NormalizedContent);
    }

    // ── Memory Type Inference ──

    [Fact]
    public void NormalizeKnowledgeDocument_PreferenceDetected()
    {
        var result = _normalizer.NormalizeKnowledgeDocument(
            "Pref",
            "I prefer using minimal APIs over controllers");

        Assert.Equal(MemoryType.UserPreference, result[0].MemoryType);
    }

    [Fact]
    public void NormalizeKnowledgeDocument_ConstraintDetected()
    {
        var result = _normalizer.NormalizeKnowledgeDocument(
            "Constraint",
            "Don't use MySQL for production databases");

        Assert.Equal(MemoryType.UserConstraint, result[0].MemoryType);
    }

    [Fact]
    public void NormalizeKnowledgeDocument_TechnicalDecisionDetected()
    {
        var result = _normalizer.NormalizeKnowledgeDocument(
            "Decision",
            "We selected Entity Framework for ORM");

        Assert.Equal(MemoryType.TechnicalDecision, result[0].MemoryType);
    }

    [Fact]
    public void NormalizeKnowledgeDocument_ArchitectureDecisionDetected()
    {
        var result = _normalizer.NormalizeKnowledgeDocument(
            "Arch",
            "Adopting clean architecture pattern for all services");

        Assert.Equal(MemoryType.ArchitectureDecision, result[0].MemoryType);
    }

    [Fact]
    public void NormalizeKnowledgeDocument_ProjectContextDetected()
    {
        var result = _normalizer.NormalizeKnowledgeDocument(
            "Context",
            "This project uses PostgreSQL and Redis");

        Assert.Equal(MemoryType.ProjectContext, result[0].MemoryType);
    }

    // ── Importance Inference ──

    [Fact]
    public void NormalizeKnowledgeDocument_ArchitectureDecisionHighImportance()
    {
        var result = _normalizer.NormalizeKnowledgeDocument(
            "Arch",
            "Adopting clean architecture pattern");

        Assert.True(result[0].Importance >= 0.8);
    }

    [Fact]
    public void NormalizeKnowledgeDocument_ConstraintHighImportance()
    {
        var result = _normalizer.NormalizeKnowledgeDocument(
            "Constraint",
            "Never use paid services");

        Assert.True(result[0].Importance >= 0.7);
    }

    // ── Developer Profile Normalization ──

    [Fact]
    public void NormalizeDeveloperProfile_CreatesRoleBioSkillsExperience()
    {
        var result = _normalizer.NormalizeDeveloperProfile(
            "Jane",
            "Senior Backend Developer",
            "Jane has 10 years of experience building distributed systems.",
            skills: ["C#", "PostgreSQL", "Docker"],
            experience: "10+ years in backend development");

        // Role + Bio + Skills + Experience = 4 candidates
        Assert.Equal(4, result.Count);

        var roleCandidate = result.First(c => c.Title.Contains("Role"));
        Assert.Contains("Jane", roleCandidate.Content);
        Assert.Contains("Senior Backend Developer", roleCandidate.Content);
        Assert.Equal(MemoryType.Fact, roleCandidate.MemoryType);
        Assert.Equal("profile:Jane", roleCandidate.Source);
        Assert.Contains("developer-profile", roleCandidate.Tags);
        Assert.Contains("role", roleCandidate.Tags);

        var skillsCandidate = result.First(c => c.Title.Contains("Skills"));
        Assert.Contains("C#", skillsCandidate.Content);
        Assert.Contains("PostgreSQL", skillsCandidate.Content);
        Assert.Contains("dotnet", skillsCandidate.Tags);
        Assert.Contains("database", skillsCandidate.Tags);
    }

    [Fact]
    public void NormalizeDeveloperProfile_MinimalInput_CreatesOnlyRole()
    {
        var result = _normalizer.NormalizeDeveloperProfile(
            "Bob",
            "Developer",
            "");

        // Only role candidate (bio is empty, no skills, no experience)
        Assert.Single(result);
        Assert.Contains("Bob", result[0].Content);
        Assert.Contains("Developer", result[0].Content);
    }

    [Fact]
    public void NormalizeDeveloperProfile_ConfidenceIsModerate()
    {
        var result = _normalizer.NormalizeDeveloperProfile(
            "Alice",
            "Architect",
            "Alice designs systems.",
            skills: ["Architecture"]);

        Assert.All(result, c => Assert.Equal(0.7, c.Confidence));
    }

    // ── Raw Normalization ──

    [Fact]
    public void NormalizeRaw_DefaultScope()
    {
        var result = _normalizer.NormalizeRaw("Test", "Some test content");

        Assert.Equal(MemoryScope.Global, result.Scope);
        Assert.Equal("raw", result.Source);
        Assert.Equal(0.5, result.Confidence);
    }

    [Fact]
    public void NormalizeRaw_ProjectScope()
    {
        var result = _normalizer.NormalizeRaw("Test", "Some content",
            projectId: Guid.NewGuid());

        Assert.Equal(MemoryScope.Project, result.Scope);
    }

    // ── Edge Cases ──

    [Fact]
    public void NormalizeKnowledgeDocument_VeryShortSection_Skipped()
    {
        var content = @"## Big Section
This is a substantial section with enough content to be included.

## Tiny
Hi.";

        var result = _normalizer.NormalizeKnowledgeDocument("Doc", content);

        // Only the big section should be included (tiny < 10 chars)
        Assert.Single(result);
        Assert.Equal("Big Section", result[0].Title);
    }

    [Fact]
    public void NormalizeKnowledgeDocument_UserTagsPreserved()
    {
        var result = _normalizer.NormalizeKnowledgeDocument(
            "Doc",
            "Content about APIs",
            tags: ["custom-tag", "important"]);

        Assert.Contains("custom-tag", result[0].Tags);
        Assert.Contains("important", result[0].Tags);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// DocumentConsolidationService tests — duplicate detection, consolidation,
// provenance, lifecycle, conflict handling
// ══════════════════════════════════════════════════════════════════════════════

public class DocumentConsolidationServiceTests
{
    private readonly Mock<IMemoryRepository> _mockRepo;
    private readonly Mock<IMemoryConflictDetector> _mockConflictDetector;
    private readonly Mock<ILogger<DocumentConsolidationService>> _mockLogger;
    private readonly DocumentConsolidationService _service;

    public DocumentConsolidationServiceTests()
    {
        _mockRepo = new Mock<IMemoryRepository>();
        _mockConflictDetector = new Mock<IMemoryConflictDetector>();
        _mockLogger = new Mock<ILogger<DocumentConsolidationService>>();

        _service = new DocumentConsolidationService(
            _mockRepo.Object,
            _mockConflictDetector.Object,
            _mockLogger.Object);
    }

    private string _ownerId = "test-owner";

    // ── Helper: Set up repository mock for search ──
    private void SetupSearchReturns(List<MemoryEntry> entries)
    {
        _mockRepo.Setup(r => r.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
    }

    private void SetupGetByScopeReturns(List<MemoryEntry> entries)
    {
        _mockRepo.Setup(r => r.GetByScopeAsync(
            It.IsAny<MemoryScope>(),
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
    }

    private void SetupCreateReturns()
    {
        _mockRepo.Setup(r => r.CreateAsync(
            It.IsAny<MemoryEntry>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemoryEntry e, CancellationToken ct) =>
            {
                e.Id = e.Id == Guid.Empty ? Guid.NewGuid() : e.Id;
                return e;
            });
    }

    // ── Exact Duplicate Detection ──

    [Fact]
    public async Task ConsolidateAsync_ExactDuplicate_ReturnsDuplicateIgnored()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL",
            Title = "DB Choice",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            NormalizedContent = "use postgresql"
        };

        SetupSearchReturns([existing]);
        SetupGetByScopeReturns([]);

        var candidate = new CanonicalMemoryCandidate
        {
            Title = "DB Choice",
            Content = "Use PostgreSQL",
            NormalizedContent = "use postgresql",
            Scope = MemoryScope.Global,
            Source = "knowledge:db"
        };

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        Assert.Equal(ConsolidationAction.DuplicateIgnored, result.Action);
        Assert.True(result.DuplicateDetected);
        Assert.Equal(existing.Id, result.MatchedMemory!.Id);
    }

    // ── Normalized Duplicate Detection ──

    [Fact]
    public async Task ConsolidateAsync_NormalizedDuplicate_ReturnsDuplicateIgnored()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL!",
            Title = "DB Choice",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            NormalizedContent = "use postgresql"
        };

        SetupSearchReturns([existing]);
        SetupGetByScopeReturns([]);

        var candidate = new CanonicalMemoryCandidate
        {
            Title = "DB Choice",
            Content = "Use PostgreSQL.",
            NormalizedContent = "use postgresql",
            Scope = MemoryScope.Global,
            Source = "knowledge:db"
        };

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        Assert.Equal(ConsolidationAction.DuplicateIgnored, result.Action);
        Assert.True(result.DuplicateDetected);
    }

    // ── New Memory Creation ──

    [Fact]
    public async Task ConsolidateAsync_NoMatch_CreatesNewMemory()
    {
        SetupSearchReturns([]);
        SetupGetByScopeReturns([]);
        SetupCreateReturns();

        var candidate = new CanonicalMemoryCandidate
        {
            Title = "DB Choice",
            Content = "Use PostgreSQL",
            NormalizedContent = "use postgresql",
            Scope = MemoryScope.Global,
            Source = "knowledge:db",
            MemoryType = MemoryType.TechnicalDecision,
            Confidence = 0.9,
            Tags = ["database"]
        };

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        Assert.Equal(ConsolidationAction.Created, result.Action);
        Assert.NotNull(result.Memory);
        Assert.Equal("Use PostgreSQL", result.Memory!.Content);
        Assert.Equal("knowledge:db", result.Memory.Source);
        Assert.Equal("test-owner", result.Memory.OwnerId);
        Assert.True(result.ProvenancePreserved);
    }

    // ── High-Similarity Supersession ──

    [Fact]
    public async Task ConsolidateAsync_UpdatedVersion_HighSimilarity_Supersedes()
    {
        // Content with high Jaccard similarity (>0.85) to trigger IsUpdatedVersion
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL for the database layer",
            Title = "DB",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            NormalizedContent = "use postgresql for the database layer"
        };

        SetupSearchReturns([existing]);
        SetupGetByScopeReturns([]);

        // 7 words, 6 shared → Jaccard 6/7 ≈ 0.857, above threshold
        var candidate = new CanonicalMemoryCandidate
        {
            Title = "DB Updated",
            Content = "Use PostgreSQL for the database layer now",
            NormalizedContent = "use postgresql for the database layer now",
            Scope = MemoryScope.Global,
            Source = "knowledge:db-v2",
            MemoryType = MemoryType.TechnicalDecision
        };

        SetupCreateReturns();

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        Assert.Equal(ConsolidationAction.SupersededExisting, result.Action);
        Assert.NotNull(result.Memory);
        Assert.NotNull(result.MatchedMemory);
        Assert.Equal(MemoryState.Superseded, existing.State);
        Assert.Equal(result.Memory!.Id, existing.SupersededById);
        Assert.True(result.ProvenancePreserved);
    }

    // ── Provenance Preservation ──

    [Fact]
    public async Task ConsolidateAsync_Supersession_PreservesProvenance()
    {
        // Use high-similarity content that triggers supersession via IsUpdatedVersion
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use Angular for the frontend rendering",
            Title = "Frontend",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            NormalizedContent = "use angular for the frontend rendering",
            Source = "knowledge:frontend-v1"
        };

        SetupSearchReturns([existing]);
        SetupGetByScopeReturns([]);

        // 8 words vs 7 shared → Jaccard 7/8 = 0.875, above threshold
        var candidate = new CanonicalMemoryCandidate
        {
            Title = "Frontend",
            Content = "Use Angular for the frontend rendering now",
            NormalizedContent = "use angular for the frontend rendering now",
            Scope = MemoryScope.Global,
            Source = "knowledge:frontend-v2",
            MemoryType = MemoryType.TechnicalDecision
        };

        SetupCreateReturns();

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        Assert.Equal(ConsolidationAction.SupersededExisting, result.Action);
        // Provenance should contain both sources
        Assert.Contains("knowledge:frontend-v2", result.Memory!.Source);
        Assert.Contains("knowledge:frontend-v1", result.Memory.Source);
    }

    // ── Conflict Handling ──

    [Fact]
    public async Task ConsolidateAsync_LowConfidenceConflict_ReturnsRequiresReview()
    {
        // Use negation pattern so FindMatch detects IsConflict
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use Angular for the frontend",
            Title = "Frontend",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            NormalizedContent = "use angular for the frontend",
            MemoryType = MemoryType.TechnicalDecision
        };

        SetupSearchReturns([existing]);
        SetupGetByScopeReturns([]);

        var candidate = new CanonicalMemoryCandidate
        {
            Title = "Frontend",
            Content = "Don't use Angular for the frontend",
            NormalizedContent = "dont use angular for the frontend",
            Scope = MemoryScope.Global,
            Source = "knowledge:frontend",
            MemoryType = MemoryType.TechnicalDecision
        };

        // Low confidence conflict — should not auto-supersede
        _mockConflictDetector.Setup(d => d.DetectConflicts(
            It.IsAny<MemoryEntry>(),
            It.IsAny<IReadOnlyList<MemoryEntry>>()))
            .Returns(new List<MemoryConflict>
            {
                new MemoryConflict
                {
                    ExistingMemory = existing,
                    ConflictType = MemoryConflictType.Contradiction,
                    Explanation = "Different frameworks",
                    ShouldSupersede = false,
                    Confidence = 0.6 // Below auto-action threshold
                }
            });

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        Assert.Equal(ConsolidationAction.RequiresReview, result.Action);
        Assert.True(result.ConflictResolved == false);
        Assert.Equal(existing.Id, result.MatchedMemory!.Id);
    }

    // ── Batch Consolidation ──

    [Fact]
    public async Task ConsolidateBatchAsync_MixedResults()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL",
            Title = "DB",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            NormalizedContent = "use postgresql"
        };

        SetupSearchReturns([existing]);
        SetupGetByScopeReturns([]);
        SetupCreateReturns();

        var candidates = new List<CanonicalMemoryCandidate>
        {
            // Exact duplicate
            new()
            {
                Title = "DB",
                Content = "Use PostgreSQL",
                NormalizedContent = "use postgresql",
                Scope = MemoryScope.Global,
                Source = "knowledge:db"
            },
            // New content
            new()
            {
                Title = "Cache",
                Content = "Use Redis for caching",
                NormalizedContent = "use redis for caching",
                Scope = MemoryScope.Global,
                Source = "knowledge:cache"
            }
        };

        var results = await _service.ConsolidateBatchAsync(candidates, _ownerId);

        Assert.Equal(2, results.Count);
        Assert.Equal(ConsolidationAction.DuplicateIgnored, results[0].Action);
        Assert.Equal(ConsolidationAction.Created, results[1].Action);
    }

    [Fact]
    public async Task ConsolidateBatchAsync_HandlesExceptionGracefully()
    {
        SetupSearchReturns([]);
        SetupGetByScopeReturns([]);

        var candidates = new List<CanonicalMemoryCandidate>
        {
            new()
            {
                Title = "Test",
                Content = "Test content",
                NormalizedContent = "test content",
                Scope = MemoryScope.Global
            }
        };

        _mockRepo.Setup(r => r.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var results = await _service.ConsolidateBatchAsync(candidates, _ownerId);

        Assert.Single(results);
        Assert.Equal(ConsolidationAction.Rejected, results[0].Action);
        Assert.Contains("DB error", results[0].Reason);
    }

    // ── FindMatch Unit Tests ──

    [Fact]
    public void FindMatch_ExactMatch_ReturnsExact()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            NormalizedContent = "use postgresql"
        };

        var candidate = new CanonicalMemoryCandidate
        {
            Content = "Use PostgreSQL",
            NormalizedContent = "use postgresql",
            Scope = MemoryScope.Global
        };

        var match = _service.FindMatch(candidate, [existing]);

        Assert.True(match.IsExactMatch);
        Assert.Equal(1.0, match.Similarity);
        Assert.Equal(existing.Id, match.BestMatch!.Id);
    }

    [Fact]
    public void FindMatch_NormalizedMatch_ReturnsNormalized()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL!",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            NormalizedContent = "use postgresql"
        };

        var candidate = new CanonicalMemoryCandidate
        {
            Content = "Use PostgreSQL.",
            NormalizedContent = "use postgresql",
            Scope = MemoryScope.Global
        };

        var match = _service.FindMatch(candidate, [existing]);

        Assert.True(match.IsNormalizedMatch);
        Assert.Equal(0.95, match.Similarity);
    }

    [Fact]
    public void FindMatch_NoMatch_ReturnsEmpty()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            NormalizedContent = "use postgresql"
        };

        var candidate = new CanonicalMemoryCandidate
        {
            Content = "Deploy to Kubernetes",
            NormalizedContent = "deploy to kubernetes",
            Scope = MemoryScope.Global
        };

        var match = _service.FindMatch(candidate, [existing]);

        Assert.Null(match.BestMatch);
        Assert.Equal(0.0, match.Similarity);
    }

    [Fact]
    public void FindMatch_SkipsDeletedMemories()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Global,
            State = MemoryState.Deleted
        };

        var candidate = new CanonicalMemoryCandidate
        {
            Content = "Use PostgreSQL",
            NormalizedContent = "use postgresql",
            Scope = MemoryScope.Global
        };

        var match = _service.FindMatch(candidate, [existing]);

        Assert.Null(match.BestMatch);
    }

    [Fact]
    public void FindMatch_SkipsSupersededMemories()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Global,
            State = MemoryState.Superseded
        };

        var candidate = new CanonicalMemoryCandidate
        {
            Content = "Use PostgreSQL",
            NormalizedContent = "use postgresql",
            Scope = MemoryScope.Global
        };

        var match = _service.FindMatch(candidate, [existing]);

        Assert.Null(match.BestMatch);
    }

    [Fact]
    public void FindMatch_DifferentScope_NoMatch()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Project,
            State = MemoryState.Active
        };

        var candidate = new CanonicalMemoryCandidate
        {
            Content = "Use PostgreSQL",
            NormalizedContent = "use postgresql",
            Scope = MemoryScope.Global
        };

        var match = _service.FindMatch(candidate, [existing]);

        Assert.Null(match.BestMatch);
    }

    // ── Rejection ──

    [Fact]
    public async Task ConsolidateAsync_EmptyContent_ReturnsRejected()
    {
        var candidate = new CanonicalMemoryCandidate
        {
            Title = "Empty",
            Content = "",
            Scope = MemoryScope.Global
        };

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        Assert.Equal(ConsolidationAction.Rejected, result.Action);
    }

    [Fact]
    public async Task ConsolidateAsync_TooShortContent_ReturnsRejected()
    {
        var candidate = new CanonicalMemoryCandidate
        {
            Title = "Short",
            Content = "ab",
            Scope = MemoryScope.Global
        };

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        Assert.Equal(ConsolidationAction.Rejected, result.Action);
    }

    // ── Provenance Tracking ──

    [Fact]
    public async Task ConsolidateAsync_NewMemory_SourcePreservedInMemory()
    {
        SetupSearchReturns([]);
        SetupGetByScopeReturns([]);
        SetupCreateReturns();

        var candidate = new CanonicalMemoryCandidate
        {
            Title = "Fact",
            Content = "The API uses .NET 10",
            NormalizedContent = "the api uses net 10",
            Scope = MemoryScope.Global,
            Source = "knowledge:tech-stack",
            MemoryType = MemoryType.Fact
        };

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        Assert.Equal(ConsolidationAction.Created, result.Action);
        Assert.Equal("knowledge:tech-stack", result.Memory!.Source);
    }

    // ── Lifecycle Interaction ──

    [Fact]
    public async Task ConsolidateAsync_SupersededMemory_IsNotConsidered()
    {
        var superseded = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Global,
            State = MemoryState.Superseded
        };

        SetupSearchReturns([superseded]);
        SetupGetByScopeReturns([]);
        SetupCreateReturns();

        var candidate = new CanonicalMemoryCandidate
        {
            Title = "DB",
            Content = "Use PostgreSQL",
            NormalizedContent = "use postgresql",
            Scope = MemoryScope.Global,
            Source = "knowledge:db"
        };

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        // Should create new memory since superseded memories are skipped
        Assert.Equal(ConsolidationAction.Created, result.Action);
    }

    // ── Project/Workspace Association ──

    [Fact]
    public async Task ConsolidateAsync_ProjectScoped_MemoryHasCorrectProjectId()
    {
        var projectId = Guid.NewGuid();
        SetupSearchReturns([]);
        SetupGetByScopeReturns([]);
        SetupCreateReturns();

        var candidate = new CanonicalMemoryCandidate
        {
            Title = "DB",
            Content = "Use PostgreSQL",
            NormalizedContent = "use postgresql",
            Scope = MemoryScope.Project,
            ProjectId = projectId,
            Source = "knowledge:db"
        };

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        Assert.Equal(ConsolidationAction.Created, result.Action);
        Assert.Equal(projectId, result.Memory!.ProjectId);
        Assert.Equal(MemoryScope.Project, result.Memory.Scope);
    }

    // ── Tags Preserved ──

    [Fact]
    public async Task ConsolidateAsync_NewMemory_TagsPreserved()
    {
        SetupSearchReturns([]);
        SetupGetByScopeReturns([]);
        SetupCreateReturns();

        var candidate = new CanonicalMemoryCandidate
        {
            Title = "Tech",
            Content = "Use PostgreSQL and Redis",
            NormalizedContent = "use postgresql and redis",
            Scope = MemoryScope.Global,
            Source = "knowledge:tech",
            Tags = ["database", "caching", "custom-tag"]
        };

        var result = await _service.ConsolidateAsync(candidate, _ownerId);

        Assert.Equal(ConsolidationAction.Created, result.Action);
        var tags = result.Memory!.Tags;
        Assert.Contains("database", tags);
        Assert.Contains("caching", tags);
        Assert.Contains("custom-tag", tags);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Integration-style tests: Normalizer → ConsolidationService pipeline
// ══════════════════════════════════════════════════════════════════════════════

public class ConsolidationPipelineTests
{
    private readonly Mock<IMemoryRepository> _mockRepo;
    private readonly Mock<IMemoryConflictDetector> _mockConflictDetector;
    private readonly Mock<ILogger<DocumentConsolidationService>> _mockLogger;
    private readonly MemoryNormalizer _normalizer;
    private readonly DocumentConsolidationService _consolidationService;

    public ConsolidationPipelineTests()
    {
        _mockRepo = new Mock<IMemoryRepository>();
        _mockConflictDetector = new Mock<IMemoryConflictDetector>();
        _mockLogger = new Mock<ILogger<DocumentConsolidationService>>();

        _normalizer = new MemoryNormalizer();
        _consolidationService = new DocumentConsolidationService(
            _mockRepo.Object, _mockConflictDetector.Object, _mockLogger.Object);

        // Default: no conflicts, create succeeds
        _mockConflictDetector.Setup(d => d.DetectConflicts(
            It.IsAny<MemoryEntry>(),
            It.IsAny<IReadOnlyList<MemoryEntry>>()))
            .Returns([]);

        _mockRepo.Setup(r => r.CreateAsync(
            It.IsAny<MemoryEntry>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemoryEntry e, CancellationToken ct) =>
            {
                e.Id = e.Id == Guid.Empty ? Guid.NewGuid() : e.Id;
                return e;
            });

        _mockRepo.Setup(r => r.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _mockRepo.Setup(r => r.GetByScopeAsync(
            It.IsAny<MemoryScope>(),
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public async Task KnowledgeDoc_ConsolidatedIntoMemory_ThroughFullPipeline()
    {
        // Step 1: Normalize a knowledge document
        var candidates = _normalizer.NormalizeKnowledgeDocument(
            "Architecture",
            "We use Clean Architecture with Domain, Application, Infrastructure, and API layers.",
            project: "DeveloperMemory",
            tags: ["architecture"]);

        Assert.NotEmpty(candidates);

        // Step 2: Consolidate each candidate
        var results = await _consolidationService.ConsolidateBatchAsync(candidates, "test-owner");

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(ConsolidationAction.Created, r.Action));
        Assert.All(results, r =>
        {
            Assert.NotNull(r.Memory);
            Assert.Contains("knowledge:", r.Memory!.Source);
            Assert.Equal("test-owner", r.Memory.OwnerId);
        });
    }

    [Fact]
    public async Task DuplicateConsolidation_DeduplicatesCorrectly()
    {
        // First consolidation: creates memory
        var candidates1 = _normalizer.NormalizeKnowledgeDocument(
            "DB",
            "Use PostgreSQL for the database");

        var results1 = await _consolidationService.ConsolidateBatchAsync(candidates1, "owner");
        Assert.All(results1, r => Assert.Equal(ConsolidationAction.Created, r.Action));

        // Now set up repository to return the created memory on next search
        var createdMemory = results1[0].Memory!;
        _mockRepo.Setup(r => r.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync([createdMemory]);

        // Second consolidation with same content: should be duplicate
        var candidates2 = _normalizer.NormalizeKnowledgeDocument(
            "DB V2",
            "Use PostgreSQL for the database");

        var results2 = await _consolidationService.ConsolidateBatchAsync(candidates2, "owner");

        Assert.All(results2, r => Assert.Equal(ConsolidationAction.DuplicateIgnored, r.Action));
    }

    [Fact]
    public async Task ProfileConsolidatedIntoMemory()
    {
        var candidates = _normalizer.NormalizeDeveloperProfile(
            "Jane",
            "Senior Backend Developer",
            "Jane builds distributed systems.",
            skills: ["C#", "PostgreSQL"],
            experience: "10 years in backend development");

        var results = await _consolidationService.ConsolidateBatchAsync(candidates, "owner");

        Assert.Equal(4, results.Count);
        Assert.All(results, r => Assert.Equal(ConsolidationAction.Created, r.Action));

        var roleMemory = results.First(r => r.Candidate!.Title.Contains("Role")).Memory!;
        Assert.Contains("Senior Backend Developer", roleMemory.Content);
        Assert.Contains("profile:", roleMemory.Source);

        var skillsMemory = results.First(r => r.Candidate!.Title.Contains("Skills")).Memory!;
        Assert.Contains("C#", skillsMemory.Content);
        Assert.Contains("dotnet", skillsMemory.Tags);
    }

    [Fact]
    public async Task ConflictingKnowledge_RequiresReview()
    {
        // Create existing memory
        // Use content that triggers negation detection in FindMatch.
        // Normalizer infers UserConstraint for "Don't use Angular for the frontend",
        // so existing must have the same MemoryType for contradiction detection.
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use Angular for the frontend",
            Title = "Frontend",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            NormalizedContent = "use angular for the frontend",
            MemoryType = MemoryType.UserConstraint
        };

        _mockRepo.Setup(r => r.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync([existing]);

        // Set up conflict detector for the RequiresReview path
        _mockConflictDetector.Setup(d => d.DetectConflicts(
            It.IsAny<MemoryEntry>(),
            It.IsAny<IReadOnlyList<MemoryEntry>>()))
            .Returns(new List<MemoryConflict>
            {
                new MemoryConflict
                {
                    ExistingMemory = existing,
                    ConflictType = MemoryConflictType.Contradiction,
                    Explanation = "Different frontend frameworks",
                    ShouldSupersede = false,
                    Confidence = 0.6
                }
            });

        // Use negation pattern so FindMatch detects IsConflict
        var candidates = _normalizer.NormalizeKnowledgeDocument(
            "Frontend V2",
            "Don't use Angular for the frontend");

        var results = await _consolidationService.ConsolidateBatchAsync(candidates, "owner");

        // Should require review (low confidence conflict)
        Assert.Contains(results, r => r.Action == ConsolidationAction.RequiresReview);
    }

    [Fact]
    public async Task MultipleKnowledgeDocs_ConsolidatedInBatch()
    {
        var candidates = new List<CanonicalMemoryCandidate>();

        candidates.AddRange(_normalizer.NormalizeKnowledgeDocument(
            "DB", "Use PostgreSQL for the database"));
        candidates.AddRange(_normalizer.NormalizeKnowledgeDocument(
            "Cache", "Use Redis for caching"));
        candidates.AddRange(_normalizer.NormalizeKnowledgeDocument(
            "Frontend", "Use Blazor for the UI"));

        var results = await _consolidationService.ConsolidateBatchAsync(candidates, "owner");

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Equal(ConsolidationAction.Created, r.Action));
    }
}
