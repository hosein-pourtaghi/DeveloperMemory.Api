# ROADMAP.md — Development Roadmap

*Last updated: 2026-08-26*

---

## Current Phase: Architecture Consolidation & Provider Abstraction

The persistent memory system, Clean Architecture foundation, OpenAI-compatible gateway, Docker deployment, and test infrastructure are all implemented. The current phase focuses on consolidating architecture boundaries, expanding test coverage, and introducing provider abstractions.

---

### Phase 1: Documentation & Vision Alignment ✅ Complete

- [x] Audit actual repository state against existing documentation
- [x] Identify contradictions between docs and source code
- [x] Update all documentation to reflect reality
- [x] Establish canonical project vision as Memory Intelligence Gateway
- [x] Document Clean Architecture structure accurately
- [x] Document test infrastructure that was previously unrecorded
- [x] Align roadmap with actual implementation status

### Phase 2: Architecture Boundary Consolidation ✅ Complete

- [x] Full 4-project Clean Architecture structure established
- [x] Domain layer with entities, enums, and repository interfaces
- [x] Application layer with services, contracts, DTOs, exceptions
- [x] Infrastructure layer with EF Core persistence, migrations, DI
- [x] API layer with controllers and gateway services
- [x] Solution file at repository root
- [x] Docker deployment (Dockerfile + docker-compose.yml)
- [x] 3 test projects with 51 test methods

### Phase 3: Build Verification & Test Expansion (Next)

**Goal:** Verify the solution compiles and tests pass. Expand test coverage to application services and API controllers.

- [ ] Verify `dotnet build` succeeds across all projects
- [ ] Verify `dotnet test` passes for all 4 test projects (~90 methods)
- [x] PromptBuilder removed — replaced by DeterministicPromptComposer in IPromptIntelligenceEngine
- [ ] Add unit tests for `ModeDetector` (plan/build/unknown detection)
- [ ] Add integration tests for `MemoryController` endpoints
- [ ] Add integration tests for `ProjectsController` endpoints
- [ ] Add integration tests for `OpenAIChatCompletionController` (with mock provider)
- [ ] Add integration tests for `KnowledgeController` endpoints

**Dependencies:** None — verification and expansion of existing infrastructure.

### Phase 4: Provider Abstraction & Replaceability ✅ Complete

- [x] Defined `IModelGateway` interface in `Api/Abstractions/`
- [x] `FreeLlmApiClient` implements `IModelGateway` as provider-specific adapter
- [x] `OpenAIChatCompletionController` depends on `IModelGateway` abstraction
- [x] `DownstreamProviderException` moved to `Api/Abstractions/`
- [x] DI registration updated: `IModelGateway` resolves to `FreeLlmApiClient`
- [x] Added `DeveloperMemory.Api.Tests` project with 15 test methods
- [x] Static validation performed (no build/test execution in Cloud Mode)

### Phase 5: Retrieval Abstraction (IMemoryRetriever) ✅ Complete

- [x] Defined `IMemoryRetriever` interface in `Api/Abstractions/`
- [x] Created `MemoryRetrievalResult` combining persistent memory + knowledge results
- [x] Implemented `ContextRetrievalService` orchestrating `IMemoryService` + `KnowledgeService`
- [x] `OpenAIChatCompletionController` depends on `IMemoryRetriever` — no longer directly coupled to `KnowledgeService` or `IMemoryService` for retrieval
- [x] DI registration: `IMemoryRetriever` resolves to `ContextRetrievalService`
- [x] Added behavioral and contract tests for the abstraction boundary
- [x] Static validation performed (no build/test execution in Cloud Mode)

### Phase 7: Prompt Intelligence Engine Foundation ✅ Complete

- [x] Defined `IPromptIntelligenceEngine` interface in `Api/Abstractions/`
- [x] Created `PromptIntelligenceResult` with `EnrichedRequest` + `SearchQuery`
- [x] Implemented `PromptIntelligenceService` orchestrating profile loading, context retrieval, and prompt assembly
- [x] Created `ManagedStream` for provider stream lifecycle management
- [x] `OpenAIChatCompletionController` simplified — delegates context/prompt intelligence to engine
- [x] DI registration: `IPromptIntelligenceEngine` resolves to `PromptIntelligenceService`
- [x] Added 14 test methods (behavioral + contract) for the engine abstraction
- [x] Static validation performed (no build/test execution in Cloud Mode)

### Phase 8: Retrieval Improvement

**Goal:** Improve retrieval quality beyond basic keyword search.

- [ ] Add lifecycle-aware filtering (exclude superseded/expired by default)
- [ ] Add importance-weighted ranking
- [ ] Add recency weighting
- [ ] Plan semantic/vector retrieval path (pgvector available in docker-compose)
- [ ] Document retrieval extension pattern

**Dependencies:** Phase 5 (abstraction boundary established).

### Phase 9: Production Readiness

**Goal:** Make the system deployable and secure.

- [ ] Add authentication/authorization middleware
- [ ] Lock down CORS for production
- [ ] Add configuration validation at startup
- [ ] Add graceful shutdown handling
- [ ] Improve structured logging (correlation IDs, request tracing)
- [ ] Add CI/CD pipeline (GitHub Actions: build, test, publish)
- [ ] Document Docker deployment properly
- [ ] Consider Redis integration for caching

**Dependencies:** Phases 3-5 (core functionality stable).

---

## V2: Intelligence Layer

These capabilities require the core architecture to be solid and well-tested before implementation.

### Memory Intelligence

- [ ] Define `IMemoryIntelligenceService` abstraction
- [ ] Candidate memory extraction from conversations
- [ ] Duplicate/similarity detection for memories
- [ ] Contradiction detection between existing and new information
- [ ] Automatic supersession decisions
- [ ] Importance evaluation (automatic vs manual)
- [ ] Selective capture policies and configuration

### Prompt Intelligence Engine (Advanced)

- [ ] Intent and task analysis (beyond current heuristic mode detection)
- [ ] Context requirements analysis per request type
- [ ] Intelligent context budget management (token limits)
- [ ] Conflict surfacing in context assembly
- [ ] Execution requirement resolution (which model, which tools)
- [ ] Optional human review pipeline

### Semantic Retrieval

- [ ] Embedding generation integration
- [ ] Vector store integration using pgvector (available in docker-compose)
- [ ] Hybrid search (keyword + semantic)
- [ ] Metadata-filtered retrieval
- [ ] Relevance feedback loops

---

## V3: Integration & Platform

### Agent Runtime & MCP

- [ ] Define `IAgentRuntime` abstraction
- [ ] Agent runtime boundary (memory/intelligence vs execution)
- [ ] MCP server integration boundary
- [ ] Tool provider abstraction
- [ ] Downstream agent consumption patterns

### Multi-User & Team

- [ ] User authentication and authorization
- [ ] Multi-tenant memory isolation
- [ ] Team-shared knowledge and memories
- [ ] Role-based access control
- [ ] Audit logging for memory changes

### Integration Ecosystem

- [ ] IDE plugin interfaces (VS Code, JetBrains)
- [ ] Webhook support for external knowledge ingestion
- [ ] Knowledge sync with documentation systems
- [ ] Analytics for context usage and effectiveness
- [ ] Plugin architecture for custom retrieval/capture strategies

---

## Architectural Evolution Principles

1. **Don't claim implemented features as future work.** The roadmap tracks what's genuinely next.
2. **Don't skip consolidation for features.** Architecture boundaries must be solid before adding intelligence.
3. **Incremental over revolutionary.** Prefer refactoring existing code into abstractions over rewriting.
4. **Replaceability first.** Before adding a new capability, ensure it can be swapped later.
5. **Provider-agnostic.** Never tightly couple core logic to one provider or vendor.
