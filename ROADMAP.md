# ROADMAP.md — Development Roadmap

*Last updated: 2026-08-25*

---

## Current Phase: Core Architecture Consolidation

The persistent memory system, Clean Architecture foundation, and OpenAI-compatible gateway are implemented. The current phase focuses on consolidating the architecture, expanding the test suite, and maturing the Prompt Intelligence building blocks.

---

### Phase 1: Documentation & Vision Alignment ✅ Complete

- [x] Audit actual repository state against existing documentation
- [x] Identify contradictions between docs and source code
- [x] Update all documentation to reflect reality
- [x] Establish canonical project vision as Memory Intelligence Gateway
- [x] Document Clean Architecture structure accurately
- [x] Document test infrastructure that was previously unrecorded
- [x] Align roadmap with actual implementation status

### Phase 2: Architecture Boundary Consolidation ✅ Complete (prior audit)

- [x] Full 4-project Clean Architecture structure established
- [x] Domain layer with entities, enums, and repository interfaces
- [x] Application layer with services, contracts, DTOs, exceptions
- [x] Infrastructure layer with EF Core persistence, migrations, DI
- [x] API layer with controllers and services

### Phase 3: Build Verification & Test Expansion (Next)

- [ ] Verify `dotnet build` succeeds across all projects
- [ ] Verify `dotnet test` passes for existing repository tests
- [ ] Add unit tests for `MemoryService` (Application layer)
- [ ] Add unit tests for `ProjectService` (Application layer)
- [ ] Add unit tests for `PromptBuilder` (context assembly, message preservation)
- [ ] Add unit tests for `ModeDetector` (plan/build/unknown detection)
- [ ] Add integration tests for `MemoryController` endpoints
- [ ] Add integration tests for `ProjectsController` endpoints
- [ ] Add integration tests for `OpenAIChatCompletionController` (with mock provider)

### Phase 4: Provider Abstraction & Replaceability

- [ ] Define `IModelGateway` interface in Application/Domain layer
- [ ] Move `FreeLlmApiClient` behind `IModelGateway` abstraction
- [ ] Enable provider swap without core logic changes
- [ ] Add configuration for alternative OpenAI-compatible providers
- [ ] Document provider replacement pattern

### Phase 5: Retrieval Improvement

- [ ] Define `IMemoryRetriever` abstraction
- [ ] Improve keyword search relevance scoring (TF-IDF or BM25-style)
- [ ] Add lifecycle-aware filtering (exclude superseded/expired from results by default)
- [ ] Add importance-weighted ranking
- [ ] Add recency weighting where appropriate
- [ ] Plan semantic/vector retrieval path (embeddings integration)

### Phase 6: Production Readiness

- [ ] Add Dockerfile for containerized deployment
- [ ] Add docker-compose.yml with PostgreSQL service
- [ ] Add CI/CD pipeline (GitHub Actions: build, test, publish)
- [ ] Add authentication/authorization middleware
- [ ] Lock down CORS for production
- [ ] Add configuration validation at startup
- [ ] Add graceful shutdown handling
- [ ] Improve structured logging (correlation IDs, request tracing)

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

### Prompt Intelligence Engine

- [ ] Define `IPromptIntelligenceEngine` abstraction
- [ ] Intent and task analysis (beyond current heuristic mode detection)
- [ ] Context requirements analysis per request type
- [ ] Intelligent context budget management (token limits)
- [ ] Conflict surfacing in context assembly
- [ ] Execution requirement resolution (which model, which tools)
- [ ] Optional human review pipeline

### Semantic Retrieval

- [ ] Embedding generation integration
- [ ] Vector store integration (replaceable provider)
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
