# AGENTS.md — AI Agent Coding Guide

*Last updated: 2026-08-26*

---

## Project Identity

DeveloperMemory.Api is a **persistent, intelligent AI memory layer and Memory Intelligence Gateway**. It is not a simple knowledge gateway or RAG application. The architecture supports lifecycle-managed persistent memory, project-scoped context, and OpenAI-compatible request enrichment.

**Source code is the primary truth.** Design documents may lag behind the implementation.

---

## Architecture Rules

1. **Clean Architecture boundaries are mandatory.** Domain ← Application ← Infrastructure ← API. Do not reverse or bypass these dependencies.

2. **Source code determines current implementation.** Do not claim features exist if they do not, and do not mark implemented features as "future work."

3. **Do not collapse the project** back into a single-project structure. The 4-project separation is intentional.

4. **Do not place business logic in the API project.** Use cases belong in Application. Business rules belong in Domain. Infrastructure implements external concerns.

5. **Keep providers replaceable.** The gateway controller depends on `IModelGateway`, not `FreeLlmApiClient`. To swap providers, implement `IModelGateway` and change the DI registration in `Program.cs`.

6. **Treat Prompt Intelligence as core architecture.** The Prompt Intelligence Engine is a central architectural capability, not a convenience helper.

7. **Do not implement blind memory capture.** Memory capture must be selective, controlled, and bounded by policies.

8. **Preserve lifecycle-aware memory design.** Memories have states (Active, Superseded, Expired, etc.) and transitions. Respect them.

9. **Distinguish implemented from planned.** Document what exists in source code, not what the vision describes as a target.

10. **Prefer incremental refactoring** over unnecessary rewrites.

---

## Project Structure

```
DeveloperMemory.Api.sln (at repository root)
│
├── src/
│   ├── DeveloperMemory.Api/
│   │   ├── Abstractions/
│   │   │   ├── IModelGateway.cs            # Provider-independent model access abstraction
│   │   │   ├── DownstreamProviderException.cs  # Provider error exception
│   │   │   ├── IMemoryRetriever.cs        # Provider-independent memory/knowledge retrieval abstraction
│   │   │   └── MemoryRetrievalResult.cs   # Combined retrieval result type
│   │   ├── Controllers/
│   │   │   ├── MemoryController.cs         # /api/Memory (CRUD, supersede, expire, stats)
│   │   │   ├── ProjectsController.cs       # /api/Projects (CRUD)
│   │   │   ├── KnowledgeController.cs      # /api/Knowledge (file-based search, CRUD)
│   │   │   ├── ProfilesController.cs       # /api/Profiles (file-based loading)
│   │   │   └── OpenAIChatCompletionController.cs  # /v1/chat/completions (gateway)
│   │   ├── Services/
│   │   │   ├── ModeDetector.cs             # Heuristic plan vs build detection
│   │   │   ├── KnowledgeService.cs         # Markdown document parsing + search
│   │   │   ├── ProfileService.cs           # Markdown profile parsing
│   │   │   ├── FreeLlmApiClient.cs         # IModelGateway implementation (OpenAI-compatible)
│   │   │   ├── ContextRetrievalService.cs  # IMemoryRetriever implementation (orchestrates memory + knowledge)
│   │   │   ├── TokenEstimator.cs           # ~4 chars/token heuristic
│   │   │   └── RequestLogger.cs            # Token metrics logging
│   │   ├── Models/                          # OpenAI types, knowledge, profiles
│   │   ├── Infrastructure/
│   │   │   ├── Configuration/               # AppSettings, ModelSelectionSettings
│   │   │   └── Middleware/                   # Exception handler, request logger
│   │   ├── Knowledge/                       # Markdown knowledge documents
│   │   └── Profiles/                        # Markdown developer profiles
│   │
│   ├── DeveloperMemory.Domain/
│   │   ├── Entities/                        # MemoryEntry, Project, BaseEntity
│   │   ├── Enums/                           # MemoryScope, MemoryState, DataClassification
│   │   └── Interfaces/                      # IMemoryRepository, IProjectRepository
│   │
│   ├── DeveloperMemory.Application/
│   │   ├── Contracts/                       # IMemoryService, IProjectService, IPromptIntelligenceEngine,
│   │   │                                    #   IMemoryRetrievalService, IContextOrchestrator, IIntentAnalyzer, etc.
│   │   ├── Services/                        # MemoryService, ProjectService, PromptIntelligence/, Retrieval/
│   │
│   └── DeveloperMemory.Infrastructure/
│       ├── Persistence/                     # DbContext, Repositories, EF Configurations
│       ├── Migrations/                      # EF Core migrations
│       └── DependencyInjection/             # ServiceCollectionExtensions
│
├── tests/
│   ├── DeveloperMemory.Domain.Tests/
│   │   └── MemoryEntryTests.cs             # 12 test methods (entity lifecycle)
│   ├── DeveloperMemory.Application.Tests/
│   │   └── MemoryServiceTests.cs           # 16 test methods (service logic)
│   ├── DeveloperMemory.Infrastructure.Tests/
│   │   ├── InMemoryDbFixture.cs             # Shared EF Core InMemory fixture
│   │   ├── MemoryRepositoryTests.cs         # 16 test methods (repository)
│   │   └── ProjectRepositoryTests.cs        # 7 test methods (repository)
│   └── DeveloperMemory.Api.Tests/
│       ├── IModelGatewayTests.cs            # 15 test methods (gateway abstraction)
│       ├── IMemoryRetrieverTests.cs         # 10 test methods (retrieval abstraction)
│       ├── IPromptIntelligenceEngineTests.cs # 16 test methods (engine abstraction)
│       ├── ModeDetectorTests.cs             # 19 test methods (mode detection)
│       ├── PromptCompositionContextTests.cs  # 8 test methods (context composition)
│       └── OpenAIChatCompletionControllerTests.cs # 7 test methods (controller orchestration)
│   └── DeveloperMemory.Tests/               # Consolidated test project (many more tests)
│
├── Dockerfile                              # Multi-stage build                              # Multi-stage build
├── docker-compose.yml                      # 4 services: api, api-postgres, postgres, redis
└── .dockerignore
```

---

## Coding Standards

### Naming
- **PascalCase** for classes, methods, properties, public fields
- **camelCase** for local variables, parameters
- **`_camelCase`** for private fields

### File Organization
- One class per file (exception: OpenAI types grouped in `OpenAIRequestResponse.cs`)
- Use file-scoped namespaces
- Nullable reference types enabled
- Use `string.Empty` not `""`
- Use `[]` collection expressions

### C# Patterns
- Pass `CancellationToken` through async chains
- Use constructor injection
- Controllers delegate to services (thin controllers)
- Return `ActionResult<T>` for type safety

### Controller Conventions
- Validate input at the controller boundary
- Delegate business logic to services
- Use domain exceptions for error responses
- Return appropriate HTTP status codes

### Service Conventions
- Services use constructor injection
- Use repository interfaces from Domain layer
- Map between entities and DTOs in the service layer

---

## How to Extend

### Adding a New Memory Capability
1. Define domain concepts in `src/DeveloperMemory.Domain`
2. Add repository interface in `src/DeveloperMemory.Domain/Interfaces`
3. Implement in `src/DeveloperMemory.Infrastructure/Persistence`
4. Add service contract in `src/DeveloperMemory.Application/Contracts`
5. Implement service in `src/DeveloperMemory.Application/Services`
6. Add controller endpoints in `src/DeveloperMemory.Api/Controllers`
7. Register in `ServiceCollectionExtensions.cs`
8. Add tests in `tests/`

### Adding a New Model/Provider
1. Create a new class implementing `IModelGateway` in `src/DeveloperMemory.Api/Services/`
2. Register it in DI by changing the `IModelGateway` registration in `Program.cs`
3. Update configuration in `AppSettings.cs` as needed
4. The controller automatically uses the new provider (no controller changes needed)

### Adding a New Knowledge Source
1. Create `.md` file in `src/DeveloperMemory.Api/Knowledge/` with YAML frontmatter
2. Use `title`, `project`, and `tags` fields (see [KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md))
3. Call `POST /api/Knowledge/reindex` to reload

---

## Key Gotchas

1. **Two memory systems coexist:** The legacy `KnowledgeService` (file-based Markdown) and the persistent `MemoryService` (PostgreSQL). Both are orchestrated behind `IMemoryRetriever` by `ContextRetrievalService`. Do not remove either without understanding the impact.

2. **Tests exist across 5 projects:** `tests/DeveloperMemory.Domain.Tests/` (10 methods), `tests/DeveloperMemory.Application.Tests/` (16 methods), `tests/DeveloperMemory.Infrastructure.Tests/` (23 methods), `tests/DeveloperMemory.Api.Tests/` (81 methods), `tests/DeveloperMemory.Tests/` (consolidated, 419 methods). Total: ~549 test methods. No integration tests for controllers or services yet.

3. **Token estimates are approximate:** ~4 chars/token heuristic. For billing-accurate counts, check `provider_tokens` in the response.

4. **Mode detection is heuristic:** Based on system prompt text analysis. Edge cases may misclassify. Set `AutoSelectModel: false` if the gateway selects the wrong model.

5. **Request log files accumulate:** Daily files in `logs/requests/`. Consider cleanup for production.

6. **CORS is wide open:** For development only. Lock down for production.

7. **Docker is available:** Dockerfile + docker-compose.yml at repository root. Use `docker compose up api` for in-memory mode, `docker compose up api-postgres` for PostgreSQL mode.

8. **Environment-bound authentication:** Development uses an auth-free local identity; Production and all non-development environments retain API-key authentication and authorization. Docker follows `ASPNETCORE_ENVIRONMENT` and does not inherently bypass security.

9. **Provider abstraction:** The controller depends on `IModelGateway` (in `Api/Abstractions/`), not the concrete `FreeLlmApiClient`. `FreeLlmApiClient` is the current OpenAI-compatible adapter. To add a new provider, implement `IModelGateway` and swap the DI registration.

10. **Retrieval abstraction:** The gateway controller depends on `IMemoryRetriever` (in `Api/Abstractions/`), not on `KnowledgeService` or `IMemoryService` directly. `ContextRetrievalService` orchestrates both retrieval sources. To change retrieval strategy, implement `IMemoryRetriever` and swap the DI registration.

11. **Prompt intelligence engine:** The controller delegates memory retrieval and optimization to `IPromptIntelligenceEngine` (defined in `DeveloperMemory.Application.Contracts`, implemented by `PromptIntelligenceEngine` in Application layer). The engine orchestrates analysis, memory retrieval, constraints, context assembly, composition, and optimization. The controller loads profiles and knowledge as formatted text and passes them into the engine, which includes them in the composed prompt. To change intelligence behavior, implement `IPromptIntelligenceEngine` and swap the DI registration.

12. **pgvector available:** docker-compose uses `pgvector/pgvector:pg16` — vector extension ready for future semantic search without infrastructure changes.

---

## Related Documentation

- [PROJECT_VISION.md](PROJECT_VISION.md) — Canonical vision and target architecture
- [CURRENT_STATUS.md](CURRENT_STATUS.md) — Verified implementation inventory
- [CLAUDE.md](CLAUDE.md) — Complete technical reference
- [KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md) — Frontmatter format reference
- [docs/ARCHITECTURE_AUDIT.md](docs/ARCHITECTURE_AUDIT.md) — Architecture audit and gap analysis
