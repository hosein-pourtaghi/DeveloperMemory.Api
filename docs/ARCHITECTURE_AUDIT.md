# ARCHITECTURE_AUDIT.md — DeveloperMemory.Api

*Generated: 2026-08-26*

---

## 1. Executive Summary

DeveloperMemory.Api is a **persistent, intelligent AI memory layer and Memory Intelligence Gateway** built on .NET 10.0 with Clean Architecture. The repository has evolved from a simple developer knowledge gateway into a multi-layer system with PostgreSQL persistence, lifecycle-managed memory, OpenAI-compatible request enrichment, and Docker deployment support.

This audit reconciles the actual codebase against the target Memory Intelligence vision and identifies concrete gaps, alignment status, and recommended evolution paths.

---

## 2. Current Architecture

### 2.1 Solution Structure

```
DeveloperMemory.Api.sln (at repository root)
│
├── src/
│   ├── DeveloperMemory.Domain/          # Core business concepts
│   ├── DeveloperMemory.Application/     # Use cases and contracts
│   ├── DeveloperMemory.Infrastructure/  # Persistence, DI, migrations
│   └── DeveloperMemory.Api/             # Controllers, gateway, services
│
├── tests/
│   ├── DeveloperMemory.Domain.Tests/        # Entity lifecycle tests (12 methods)
│   ├── DeveloperMemory.Application.Tests/   # Service logic tests (16 methods)
│   ├── DeveloperMemory.Infrastructure.Tests/ # Repository tests (23 methods)
│   └── DeveloperMemory.Api.Tests/           # Gateway + retrieval tests (~25 methods)
│
├── Dockerfile                    # Multi-stage build
├── docker-compose.yml            # 4 services (api, api-postgres, postgres, redis)
├── .dockerignore
└── docs/
    └── ARCHITECTURE_AUDIT.md     # This document
```

**Total projects:** 8 (4 source + 4 test)
**Total source files:** ~78 across all source projects
**Total test methods:** ~90 across 4 test projects

### 2.2 Dependency Direction

```
Domain  ←  Application  ←  Infrastructure  ←  Api
(no deps)    (Domain)      (Domain, App)     (all three)
```

Clean Architecture boundaries are properly enforced. The Domain project has zero external dependencies. Application depends only on Domain. Infrastructure implements persistence and DI. Api is the composition root.

### 2.3 Domain Layer (DeveloperMemory.Domain)

**Entities:**
- `BaseEntity` — Guid primary key
- `MemoryEntry` — Full lifecycle model with Title, Content, Scope, State, Classification, Importance, Tags, Source, ProjectId, SupersededById, ExpiresAt, MetadataJson. Domain methods: `SetTags()`, `Supersede()`, `Expire()`, `Archive()`, `SoftDelete()`. Computed: `IsExpired`, `IsActive`.
- `Project` — Name (unique), Description, ConfigurationJson, navigation to Memories collection.

**Enums:**
- `MemoryScope` — Global, Project, Workspace, Private
- `MemoryState` — Active, Updated, Superseded, Expired, Archived, Deleted
- `DataClassification` — Public, Internal, Confidential, Secret

**Interfaces:**
- `IMemoryRepository` — GetById, GetByScope, Search, GetExpired, Create, Update, Delete, Count
- `IProjectRepository` — GetById, GetAll, Create, Update, Delete

### 2.4 Application Layer (DeveloperMemory.Application)

**Contracts:**
- `IMemoryService` — Full CRUD + Supersede, Expire, Stats, Search with tags
- `IProjectService` — Full CRUD with memory count

**Services:**
- `MemoryService` — Complete implementation. Validates project scope requires ProjectId, soft delete, supersession creates replacement + marks old as superseded, expiration batch processing, statistics aggregation.
- `ProjectService` — Complete implementation with memory count mapping.

**DTOs:** Create/Update/Response DTOs for Memory and Project, MemoryStatsDto.

**Exceptions:** DomainException, MemoryNotFoundException, ProjectNotFoundException.

### 2.5 Infrastructure Layer (DeveloperMemory.Infrastructure)

**Persistence:**
- `DeveloperMemoryDbContext` — EF Core DbContext with MemoryEntries and Projects DbSets
- `MemoryRepository` — PostgreSQL implementation with keyword search (Contains), scope filtering, project filtering, deleted-entry exclusion, expired entry queries
- `ProjectRepository` — Standard CRUD with EF Core

**Configurations:**
- `MemoryEntryConfiguration` — Table mapping with 8 indexes (Scope, State, ProjectId, Classification, CreatedAt, ExpiresAt, composite Scope+ProjectId+State, SupersededById), foreign keys with restrict/set-null behaviors
- `ProjectConfiguration` — Table mapping with unique Name index

**Migrations:** InitialCreate (2026-08-24) creating MemoryEntries and Projects tables

**DI:** `ServiceCollectionExtensions` — Registers PostgreSQL or InMemory, repositories, application services

### 2.6 API Layer (DeveloperMemory.Api)

**Controllers (5):**
- `MemoryController` (`/api/Memory`) — CRUD, supersede, expire, stats
- `ProjectsController` (`/api/Projects`) — CRUD
- `KnowledgeController` (`/api/Knowledge`) — File-based search, CRUD, reindex
- `ProfilesController` (`/api/Profiles`) — File-based loading
- `OpenAIChatCompletionController` (`/v1`) — Gateway with streaming, enrichment, mode detection, model selection

**Gateway Services (8):**
- `IMemoryRetriever` — Provider-independent retrieval abstraction for context assembly
- `ContextRetrievalService` — Orchestrates persistent memory + knowledge document retrieval
- `DeterministicPromptComposer` — Provider-neutral prompt composition including profiles, knowledge, and intelligence context (replaces PromptBuilder)
- `ModeDetector` — Heuristic detection of plan vs build mode from system prompt text
- `KnowledgeService` — Markdown/YAML frontmatter parsing, keyword search with relevance scoring
- `ProfileService` — Markdown/YAML frontmatter parsing, profile loading
- `FreeLlmApiClient` — HTTP client for OpenAI-compatible providers. Streaming + non-streaming, model resolution, model listing.
- `TokenEstimator` — ~4 chars/token heuristic
- `RequestLogger` — Three-phase token logging (INCOMING → ENRICHED → RESPONSE)

**Middleware:**
- `GlobalExceptionMiddleware` — OpenAI-compatible error responses for /v1/*, RFC7807 for others
- `RequestLoggingMiddleware` — Diagnostic request body logging for /v1/* POST endpoints

**Configuration:** Strongly-typed AppSettings with FreeLlmApi, Paths, ModelSelection sections

**OpenTelemetry:** Configurable traces, metrics, logs (disabled by default)

**Observability:** Serilog console + rolling file, OpenTelemetry integration

### 2.7 Test Infrastructure

| Test Project | Framework | Test Methods | Focus |
|---|---|---|---|
| DeveloperMemory.Domain.Tests | xUnit 2.9.3 | 12 | MemoryEntry lifecycle, scopes, states, tags, expiration |
| DeveloperMemory.Application.Tests | xUnit 2.9.3 | 16 | MemoryService CRUD, supersession, expiration, stats, validation |
| DeveloperMemory.Infrastructure.Tests | xUnit 2.9.3 | 23 | MemoryRepository + ProjectRepository with EF Core InMemory |
| DeveloperMemory.Api.Tests | xUnit 2.9.3 | ~39 | IModelGateway, IMemoryRetriever, IPromptIntelligenceEngine |

**Total: ~90 test methods** across 4 projects (12 + 16 + 23 + ~39).

All test projects use:
- `Microsoft.NET.Test.Sdk` 17.13.0
- `xunit.runner.visualstudio` 2.8.2
- `coverlet.collector` 6.0.4
- `Microsoft.EntityFrameworkCore.InMemory` 10.0.0 (Infrastructure.Tests only)

### 2.8 Docker Infrastructure

**Dockerfile:** Multi-stage build
- Stage 1 (build): `mcr.microsoft.com/dotnet/sdk:10.0` — restore, publish
- Stage 2 (runtime): `mcr.microsoft.com/dotnet/aspnet:10.0` — published output, profiles, knowledge
- Defaults to in-memory database; configurable via environment variables

**docker-compose.yml:** 4 services
| Service | Image | Purpose |
|---|---|---|
| `api` | Built from Dockerfile | In-memory mode, development |
| `api-postgres` | Built from Dockerfile | PostgreSQL mode, production-like |
| `postgres` | `pgvector/pgvector:pg16` | PostgreSQL with pgvector extension |
| `redis` | `redis:7-alpine` | Cache (available, not yet integrated) |

**Key detail:** The PostgreSQL image is `pgvector/pgvector:pg16`, providing vector extension capability for future semantic search without changing infrastructure.

---

## 3. Gap Analysis

### 3.1 Already Aligned with Target Architecture

| Area | Status | Evidence |
|---|---|---|
| Clean Architecture | ✅ Complete | 4-project structure with correct dependency direction |
| Domain model | ✅ Complete | MemoryEntry with full lifecycle, Project with association |
| Repository pattern | ✅ Complete | IMemoryRepository, IProjectRepository in Domain |
| PostgreSQL persistence | ✅ Complete | EF Core with migrations, proper table configuration |
| Lifecycle states | ✅ Complete | Active, Updated, Superseded, Expired, Archived, Deleted |
| Memory scopes | ✅ Complete | Global, Project, Workspace, Private |
| Data classification | ✅ Complete | Public, Internal, Confidential, Secret |
| Manual supersession | ✅ Complete | SupersedeAsync creates replacement + marks old |
| Expiration processing | ✅ Complete | Batch expire via API endpoint |
| Soft deletion | ✅ Complete | State-based, not physical delete |
| Memory statistics | ✅ Complete | Counts by scope and state |
| Project-scoped memory | ✅ Complete | FK relationship, scoped queries |
| OpenAI-compatible gateway | ✅ Complete | Streaming, non-streaming, models endpoint |
| Context enrichment | ✅ Complete | Profiles + knowledge + persistent memory |
| Docker deployment | ✅ Complete | Dockerfile + docker-compose with PostgreSQL |
| Test infrastructure | ✅ Complete | 4 test projects, ~90 test methods |
| Observability foundation | ✅ Complete | OpenTelemetry + Serilog configured |

### 3.2 Partially Aligned (Functional but Limited)

| Area | Current State | Gap |
|---|---|---|
| **PromptBuilder** | **Removed in Phase 12** — replaced by `DeterministicPromptComposer` in the `IPromptIntelligenceEngine` pipeline | Context assembly now handled by engine with profile/knowledge support |
| **ModeDetector** | Heuristic keyword matching in system prompt text | No real intent analysis; misclassifies edge cases; specific to Cline-style prompts |
| **Keyword search** | EF Core Contains() on Title, Content, TagsJson | No semantic understanding, no TF-IDF/BM25, no vector search |
| **FreeLlmApiClient** | Implements IModelGateway behind provider abstraction | Currently the only provider implementation; swapping requires only DI registration change |
| **KnowledgeService** | File-based Markdown with keyword search | IDs regenerate on load; no vector search; frontmatter parser limited |
| **TokenEstimator** | ~4 chars/token heuristic | Not billing-accurate; sufficient for logging |

### 3.3 Misaligned or Missing from Target

| Area | Issue | Priority |
|---|---|---|
| **Gateway services in API project** | ModeDetector, FreeLlmApiClient live in Api project. Conceptually belong in Application or Infrastructure layers. | Medium |
| **IPromptIntelligenceEngine (basic)** | Interface and basic orchestration implemented (Phase 7). Full intent analysis, context budget, conflict detection still target architecture. | Medium (target) |
| **No IMemoryIntelligenceService** | No automatic memory evaluation, duplicate detection, or contradiction analysis. | Medium (target) |
| **Environment-bound authentication** | Development uses a local identity without developer credentials; Production/non-Development retain API-key authentication and authorization. | Resolved |
| **Redis not integrated** | Redis service defined in docker-compose but not used by application code. | Low |
| **No CI/CD** | No GitHub Actions or build automation pipeline. | Low |

### 3.4 Not Yet Implemented (Target Architecture)

| Area | Description |
|---|---|
| **Semantic/vector retrieval** | Embedding generation, vector store, hybrid search |
| **Automatic memory capture** | Conversation extraction, candidate evaluation, selective storage |
| **Contradiction detection** | Automatic detection of conflicting memories |
| **Full Prompt Intelligence** | Intent analysis, context budget management, conflict surfacing, execution requirements (beyond basic orchestration) |
| **IAgentRuntime** | Agent execution boundary abstraction |
| **MCP/tool integration** | Model Context Protocol server/tool providers |
| **Multi-user support** | Authentication, authorization, tenant isolation |
| **CI/CD pipeline** | Automated build, test, deploy |

---

## 4. Target Architecture Direction

### 4.1 Established Abstractions (Phase 4-5)

```
External Clients
        │
        ▼
API Layer (Controllers, Middleware)
        │
        ▼
Application Layer
        ├── IModelGateway (abstraction for LLM providers)
        ├── IMemoryRetriever (pluggable retrieval strategies)
        ├── IPromptIntelligenceEngine (context assembly + planning)
        └── IMemoryService, IProjectService (existing)
        │
        ▼
Domain Layer
        ├── Entities (MemoryEntry, Project)
        ├── Enums (MemoryScope, MemoryState, DataClassification)
        └── Interfaces (IMemoryRepository, IProjectRepository, + new abstractions)
        │
        ▼
Infrastructure Layer
        ├── PostgreSQL (existing)
        ├── OpenAI-compatible adapter (FreeLlmApiClient behind IModelGateway)
        ├── Keyword retrieval (existing, behind IMemoryRetriever)
        └── Future: Vector retrieval, embedding providers
```

### 4.2 Medium-Term Evolution (Phase 5-6)

```
Request
    │
    ▼
Intent & Task Analysis (beyond heuristic mode detection)
    │
    ▼
Prompt Intelligence Engine
    ├── Context Requirements Analysis
    ├── Memory Retrieval Planning
    ├── Project Context Resolution
    ├── Rules and Constraint Resolution
    ├── Lifecycle-aware Retrieval
    ├── Ranking (importance, recency, relevance)
    ├── Context Budget Management
    ├── Conflict/Supercession Surfacing
    └── Execution Requirement Resolution
    │
    ▼
Execution Package
    │
    ▼
Agent Runtime / Model / Tools / MCP
    │
    ▼
Response
    │
    ▼
Optional Selective Memory Capture
```

### 4.3 Architectural Principles for Evolution

1. **Introduce abstractions when they protect meaningful boundaries** — not for theoretical purity
2. **FreeLlmApiClient → IModelGateway** is the highest-priority abstraction because it enables provider swap
3. **IMemoryRetriever** is the second priority because it enables semantic/vector search later
4. **IPromptIntelligenceEngine** is a longer-term goal that builds on the above
5. **Do not move gateway services prematurely** — the current API-layer placement works; move when the abstraction boundaries are clear

---

## 5. Recommended Implementation Phases

### Phase 3: Build Verification & Test Expansion

**Goal:** Verify the solution builds and tests pass. Expand test coverage to application services and API controllers.

**Changes:**
- Verify `dotnet build` succeeds across all projects
- Verify `dotnet test` passes for all 3 test projects
- ✅ PromptBuilder removed; replaced by DeterministicPromptComposer in IPromptIntelligenceEngine pipeline
- Add unit tests for `ModeDetector` (mode detection heuristics)
- Add integration tests for `MemoryController` endpoints
- Add integration tests for `ProjectsController` endpoints
- Add integration tests for `OpenAIChatCompletionController` (with mock provider)

**Dependencies:** None — this is verification and expansion of existing test infrastructure.

### Phase 4: Provider Abstraction & Replaceability

**Goal:** Decouple the LLM provider from the gateway controller.

**Changes:**
- Define `IModelGateway` interface in Application layer
- Move `FreeLlmApiClient` behind `IModelGateway` (or create adapter)
- Update `OpenAIChatCompletionController` to depend on `IModelGateway`
- Enable configuration-based provider swap
- Add integration tests with mock provider

**Dependencies:** Phase 3 (build verification must pass first).

### Phase 5: Retrieval Improvement

**Goal:** Make retrieval pluggable and improve relevance.

**Changes:**
- Define `IMemoryRetriever` abstraction in Domain/Application
- Implement `KeywordMemoryRetriever` wrapping existing search logic
- Add lifecycle-aware filtering (exclude superseded/expired by default)
- Add importance-weighted ranking
- Add recency weighting
- Plan semantic/vector retrieval path (pgvector already available in docker-compose)

**Dependencies:** Phase 4 (abstraction patterns established).

### Phase 6: Production Readiness

**Goal:** Make the system deployable and secure.

**Changes:**
- Add authentication/authorization middleware
- Lock down CORS for production
- Add configuration validation at startup
- Add graceful shutdown handling
- Improve structured logging (correlation IDs)
- Add CI/CD pipeline (GitHub Actions)
- Document Docker deployment properly

**Dependencies:** Phases 3-5 (core functionality stable).

---

## 6. Architecture Decisions

### Decision 1: Preserve gateway services in API project (for now)

**Context:** PromptBuilder, ModeDetector, FreeLlmApiClient are in the API project, not Application or Infrastructure.

**Decision:** Keep them in the API project for now. Moving them prematurely would require defining abstractions that don't yet have clear boundaries. When IModelGateway and IPromptIntelligenceEngine are introduced (Phase 4-5), the services can move behind those abstractions in the appropriate layer.

**Rationale:** Premature abstraction is worse than slightly misplaced concrete implementations.

### Decision 2: pgvector in docker-compose is forward-looking

**Context:** docker-compose uses `pgvector/pgvector:pg16` instead of plain `postgres:16`.

**Decision:** This is intentional. The pgvector extension provides vector search capability without requiring a separate vector database. When semantic retrieval is implemented (Phase 5), the infrastructure is already in place.

### Decision 3: Three test projects are appropriate

**Context:** Tests are split across Domain, Application, and Infrastructure test projects.

**Decision:** This matches the Clean Architecture layering. Domain tests verify entity behavior. Application tests verify service logic with mocked repositories. Infrastructure tests verify persistence with EF Core InMemory. This separation allows testing each layer independently.

### Decision 4: Redis is available but not integrated

**Context:** Redis service exists in docker-compose but is not used by application code.

**Decision:** Redis is provisioned for future use (caching, rate limiting, session management). It should not be integrated until a concrete caching need is identified.

---

## 7. Testing Status

**NOT EXECUTED: .NET build and tests could not be run in FreeBuff Cloud Mode.**

Static analysis of test code:
- All test projects target `net10.0`
- Test projects reference correct source projects
- InMemoryDbFixture creates isolated EF Core InMemory contexts
- Test methods cover CRUD, lifecycle transitions, search, filtering, validation, and error cases
- No compilation errors detected in static review (namespace consistency, reference direction, constructor injection all correct)

**Requirement:** Before considering this audit complete, `dotnet build` and `dotnet test` must be verified in a .NET-capable environment.

---

## 8. Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Tests may not compile due to .NET 10.0 SDK version | Medium | Verify in .NET-capable environment |
| FreeLlmApiClient coupling limits provider swap | Medium | Phase 4 introduces IModelGateway |
| Keyword search quality insufficient for production | Medium | Phase 5 introduces retrieval abstraction |
| Production authentication boundary | Resolved | API-key authentication is enabled outside Development; Docker defaults to Production and follows `ASPNETCORE_ENVIRONMENT` |
| Knowledge document ID instability | Low | Known limitation, documented |
| CORS wide open | High for production | Phase 6 locks down CORS |
