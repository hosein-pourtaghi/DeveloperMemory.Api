# Changelog

## [7.0.0] - 2026-08-26

### Phase 7: Prompt Intelligence Engine Foundation

- **Introduced `IPromptIntelligenceEngine` interface** (`src/DeveloperMemory.Api/Abstractions/IPromptIntelligenceEngine.cs`) — core architectural boundary for prompt/context intelligence with single method `PreparePromptAsync`
- **Created `PromptIntelligenceResult`** (`src/DeveloperMemory.Api/Abstractions/PromptIntelligenceResult.cs`) — result type containing `EnrichedRequest` and `SearchQuery` metadata
- **Implemented `PromptIntelligenceService`** (`src/DeveloperMemory.Api/Services/PromptIntelligenceService.cs`) — orchestrates profile loading, context retrieval (via `IMemoryRetriever`), and prompt assembly (via `PromptBuilder`)
- **Created `ManagedStream`** (`src/DeveloperMemory.Api/Abstractions/ManagedStream.cs`) — internal stream wrapper managing provider stream lifecycle
- **`OpenAIChatCompletionController` simplified** — delegates context retrieval and prompt assembly to `IPromptIntelligenceEngine`; no longer directly depends on `ProfileService`, `PromptBuilder`, or `IMemoryRetriever`
- **DI registration updated** — `IPromptIntelligenceEngine` resolves to `PromptIntelligenceService`; changing intelligence behavior requires only a DI change
- **Added `IPromptIntelligenceEngineTests.cs`** with behavioral tests (via `InMemoryPromptIntelligenceEngine`) and contract/reflection tests (14 methods)
- **Preserved existing behavior** — all gateway functionality, streaming, model selection, mode detection, token logging unchanged
- **Documentation updated** across CURRENT_STATUS, ROADMAP, CLAUDE, AGENTS, CHANGELOG

## [4.0.0] - 2026-08-26

### Phase 4: Provider Abstraction (IModelGateway)

- **Introduced `IModelGateway` interface** (`src/DeveloperMemory.Api/Abstractions/IModelGateway.cs`) — provider-independent abstraction for LLM/model access
- **`FreeLlmApiClient` now implements `IModelGateway`** — current OpenAI-compatible adapter behind the abstraction
- **`DownstreamProviderException` moved** to `DeveloperMemory.Api.Abstractions` namespace
- **`OpenAIChatCompletionController` depends on `IModelGateway`** — no longer directly coupled to `FreeLlmApiClient`
- **DI registration updated** — `IModelGateway` resolves to `FreeLlmApiClient`; swapping providers requires only a DI change in `Program.cs`
- **Added `DeveloperMemory.Api.Tests`** project with 15 test methods covering:
  - `IModelGateway` contract behavior (via `InMemoryModelGateway`)
  - `FreeLlmApiClient` interface compliance
  - `DownstreamProviderException` behavior
- **Solution file updated** to include the new test project (4 test projects total)
- **Documentation updated** across CURRENT_STATUS, ROADMAP, CLAUDE, AGENTS, CHANGELOG

## [5.0.0] - 2026-08-26

### Phase 5: Retrieval Abstraction (IMemoryRetriever)

- **Introduced `IMemoryRetriever` interface** (`src/DeveloperMemory.Api/Abstractions/IMemoryRetriever.cs`) — provider-independent abstraction for retrieving relevant memory and knowledge context
- **Created `MemoryRetrievalResult`** (`src/DeveloperMemory.Api/Abstractions/MemoryRetrievalResult.cs`) — combined result type holding `List<MemoryDto>` and `List<SearchResult>`
- **Implemented `ContextRetrievalService`** (`src/DeveloperMemory.Api/Services/ContextRetrievalService.cs`) — orchestrates persistent memory search (via `IMemoryService`) and knowledge document search (via `KnowledgeService`) behind the abstraction
- **`OpenAIChatCompletionController` now depends on `IMemoryRetriever`** — controller no longer directly depends on `KnowledgeService` or `IMemoryService` for retrieval; retrieval is a single call returning combined context
- **DI registration updated** — `IMemoryRetriever` resolves to `ContextRetrievalService`; changing retrieval strategy requires only a DI change
- **Added `IMemoryRetrieverTests.cs`** with behavioral tests (via `InMemoryMemoryRetriever`) and contract/reflection tests
- **Preserved existing behavior** — memory retrieval error tolerance, knowledge search scoring, and prompt assembly all unchanged
- **Documentation updated** across CURRENT_STATUS, ROADMAP, CHANGELOG

---

## [3.1.0] - 2026-08-26

### Architecture Audit & Documentation Correction

- **Major documentation corrections:** Previous documentation contained significant inaccuracies:
  - Claimed "Docker does not exist" — Dockerfile, docker-compose.yml, and .dockerignore all present and functional
  - Claimed "1 test project" — Actually 3 test projects with 51 test methods total
  - Claimed "no tests exist" — 51 xUnit test methods across Domain, Application, and Infrastructure test projects
  - Claimed "no solution file" — `DeveloperMemory.Api.sln` exists at repository root
  - Failed to document pgvector in docker-compose PostgreSQL image
  - Failed to document Redis service in docker-compose
  - Claimed tests directory was empty — contains 3 complete test project directories
- **Architecture Audit document added** (`docs/ARCHITECTURE_AUDIT.md`) — comprehensive gap analysis between current implementation and target Memory Intelligence vision
- **All 8 documentation files updated** to match actual repository state
- **Docker documentation added** to README, CLAUDE.md, AGENTS.md, CURRENT_STATUS.md
- **Test documentation corrected** across all files — 3 projects, 51 methods accurately documented
- **pgvector and Redis** infrastructure documented as available but not yet integrated

---

## [3.0.0] - 2026-08-25

### Comprehensive Documentation & Vision Alignment Update

- **Full documentation rewrite** across all 8 documentation files to align with actual source code
- **Corrected major contradictions** between documentation and implementation:
  - Documentation previously claimed "no tests exist" — test project `tests/DeveloperMemory.Infrastructure.Tests/` with xUnit tests now documented
  - Documentation previously described only a single-project file-based system — 4-project Clean Architecture now accurately documented
  - Documentation previously claimed "persistent database storage is future work" — PostgreSQL + EF Core persistence is documented as implemented
  - Documentation previously listed implemented features (memory lifecycle, project model, supersession) as "future V2" work
- **Established canonical project identity** as "Persistent Intelligent AI Memory Layer / Memory Intelligence Gateway" across all documents
- **Separated implemented vs planned** capabilities clearly in all documentation
- **Added ROADMAP.md** with accurate phase tracking
- **Added KNOWLEDGE_FORMAT.md** with frontmatter reference
- **Added DOCUMENTATION.md** as documentation index
- **Updated CHANGELOG.md** with version history

---

## [2.0.0] - 2026-08-24

### Clean Architecture & Persistent Memory

- **4-project Clean Architecture** structure established (Domain, Application, Infrastructure, Api)
- **MemoryEntry domain model** with lifecycle states, scopes, classifications, importance, tags
- **Project domain model** for scoped memory association
- **PostgreSQL persistence** via Entity Framework Core with migrations
- **Memory management APIs** — full CRUD, supersede, expire, search, statistics
- **OpenAI-compatible gateway** with streaming, context enrichment, mode detection, model selection
- **Token tracking and logging** — three-phase estimation with daily file logging
- **Repository layer tests** with xUnit and EF Core InMemory

---

## [1.0.0] - 2026-08-23

### Initial Developer Knowledge Gateway

- **Markdown knowledge documents** with YAML frontmatter
- **Developer profile loading** from Markdown files
- **Keyword-based search** with relevance scoring
- **OpenAI-compatible proxy** with request forwarding
- **PromptBuilder** for context enrichment
- **ModeDetector** for plan vs build heuristic detection
- **FreeLlmApiClient** for OpenAI-compatible provider communication
