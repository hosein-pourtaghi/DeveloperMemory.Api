# CLAUDE.md — DeveloperMemory.Api

## Project Overview

DeveloperMemory.Api is a **persistent, intelligent AI memory layer and Memory Intelligence Gateway**. It enables AI systems to remember relevant information across conversations and provide contextually appropriate responses.

**Core purpose:** Prevent developers and AI coding assistants from repeatedly needing to rediscover or manually provide important context.

**Core architecture:** Clean Architecture with 4 source projects + 4 test projects, PostgreSQL persistence via EF Core, Docker deployment, and an OpenAI-compatible gateway with context enrichment. The gateway uses an `IModelGateway` abstraction for provider-independent model access.

**Source code is the authority** for current implementation status. Target architecture is documented in [PROJECT_VISION.md](PROJECT_VISION.md).

---

## Repository Structure

```
DeveloperMemory.Api.sln (at repository root)
│
├── src/
│   ├── DeveloperMemory.Domain/           # Entities, enums, repository interfaces
│   │   ├── Entities/
│   │   │   ├── BaseEntity.cs
│   │   │   ├── MemoryEntry.cs
│   │   │   └── Project.cs
│   │   ├── Enums/
│   │   │   ├── DataClassification.cs     # Public, Internal, Confidential, Secret
│   │   │   ├── MemoryScope.cs            # Global, Project, Workspace, Private
│   │   │   └── MemoryState.cs            # Active, Updated, Superseded, Expired, Archived, Deleted
│   │   └── Interfaces/
│   │       ├── IMemoryRepository.cs
│   │       └── IProjectRepository.cs
│   │
│   ├── DeveloperMemory.Application/      # Use cases, contracts, DTOs
│   │   ├── Contracts/
│   │   │   ├── IMemoryService.cs
│   │   │   └── IProjectService.cs
│   │   ├── Services/
│   │   │   ├── MemoryService.cs
│   │   │   └── ProjectService.cs
│   │   ├── DTOs/
│   │   │   ├── CreateMemoryRequest.cs
│   │   │   ├── UpdateMemoryRequest.cs
│   │   │   ├── MemoryDto.cs
│   │   │   ├── MemoryStatsDto.cs
│   │   │   ├── CreateProjectRequest.cs
│   │   │   ├── UpdateProjectRequest.cs
│   │   │   └── ProjectDto.cs
│   │   └── Exceptions/
│   │       ├── DomainException.cs
│   │       ├── MemoryNotFoundException.cs
│   │       └── ProjectNotFoundException.cs
│   │
│   ├── DeveloperMemory.Infrastructure/   # EF Core, persistence, DI
│   │   ├── Persistence/
│   │   │   ├── DeveloperMemoryDbContext.cs
│   │   │   ├── MemoryRepository.cs
│   │   │   ├── ProjectRepository.cs
│   │   │   └── Configurations/
│   │   │       ├── MemoryEntryConfiguration.cs
│   │   │       └── ProjectConfiguration.cs
│   │   ├── Migrations/
│   │   │   └── 20260824182548_InitialCreate.cs
│   │   └── DependencyInjection/
│   │       └── ServiceCollectionExtensions.cs
│   │
│   └── DeveloperMemory.Api/              # Controllers, services, gateway
│       ├── Abstractions/
│       │   ├── IModelGateway.cs           # Provider-independent model access
│       │   ├── DownstreamProviderException.cs
│       │   ├── IMemoryRetriever.cs        # Provider-independent retrieval abstraction
│       │   ├── MemoryRetrievalResult.cs   # Combined retrieval result type
│       │   ├── IPromptIntelligenceEngine.cs # Core prompt intelligence boundary
│       │   ├── PromptIntelligenceResult.cs # Engine result type
│       │   └── ManagedStream.cs           # Stream wrapper for provider lifecycle
│       ├── Controllers/
│       │   ├── MemoryController.cs
│       │   ├── ProjectsController.cs
│       │   ├── KnowledgeController.cs
│       │   ├── ProfilesController.cs
│       │   └── OpenAIChatCompletionController.cs
│       ├── Services/
│       │   ├── PromptBuilder.cs
│       │   ├── ModeDetector.cs
│       │   ├── KnowledgeService.cs
│       │   ├── ProfileService.cs
│       │   ├── FreeLlmApiClient.cs
│       │   ├── ContextRetrievalService.cs  # IMemoryRetriever implementation
│       │   ├── PromptIntelligenceService.cs # IPromptIntelligenceEngine implementation
│       │   ├── TokenEstimator.cs
│       │   └── RequestLogger.cs
│       ├── Models/                       # OpenAI types, knowledge, profiles
│       ├── Infrastructure/               # Configuration, Middleware
│       ├── Knowledge/                    # Markdown knowledge documents
│       └── Profiles/                     # Markdown developer profiles
│
├── tests/
│   ├── DeveloperMemory.Domain.Tests/
│   │   └── MemoryEntryTests.cs           # 12 test methods
│   ├── DeveloperMemory.Application.Tests/
│   │   └── MemoryServiceTests.cs         # 16 test methods
│   ├── DeveloperMemory.Infrastructure.Tests/
│   │   ├── InMemoryDbFixture.cs
│   │   ├── MemoryRepositoryTests.cs      # 16 test methods
│   │   └── ProjectRepositoryTests.cs     # 7 test methods│       └── DeveloperMemory.Api.Tests/
│       ├── IModelGatewayTests.cs         # 15 test methods
│       ├── IMemoryRetrieverTests.cs      # 10 test methods
│       └── IPromptIntelligenceEngineTests.cs # 14 test methods
│
├── Dockerfile                            # Multi-stage build
├── docker-compose.yml                    # 4 services: api, api-postgres, postgres, redis
├── .dockerignore
└── docs/
    └── ARCHITECTURE_AUDIT.md             # Architecture audit and gap analysis
```

---

## Architecture

### Clean Architecture (Implemented)

```
Domain ← Application ← Infrastructure ← API
```

- **Domain** contains core business concepts (entities, enums, repository interfaces). No dependencies on other projects.
- **Application** contains use cases (services), contracts (service interfaces), DTOs, and exceptions. Depends only on Domain.
- **Infrastructure** implements persistence (EF Core, repositories), DI registration. Depends on Domain and Application.
- **API** exposes transport (controllers), composition (Program.cs), and gateway services. Depends on all others.

### Current Request Flow

```
AI Client Request
        │
        ▼
OpenAIChatCompletionController
        │
        ├── Validate request
        ├── Detect mode (ModeDetector)
        ├── Select model (auto or client-specified)
        ├── Prepare enriched prompt (IPromptIntelligenceEngine → PromptIntelligenceService)
        │     ├── Load developer profiles (ProfileService)
        │     ├── Retrieve context (IMemoryRetriever → ContextRetrievalService)
        │     │     ├── Search persistent memory (IMemoryService)
        │     │     └── Search knowledge documents (KnowledgeService)
        │     └── Build enriched request (PromptBuilder)
        ├── Log token metrics (TokenEstimator + RequestLogger)
        ├── Forward to provider (IModelGateway → FreeLlmApiClient)
        │     ├── Non-streaming: await response, serialize
        │     └── Streaming: pipe SSE stream to client
        └── Log response metrics
```

### Provider Abstraction

The controller depends on `IModelGateway` (in `Api/Abstractions/`), not the concrete `FreeLlmApiClient`. To swap providers:
1. Create a new class implementing `IModelGateway`
2. Change the DI registration in `Program.cs`

### Retrieval Abstraction

The controller depends on `IMemoryRetriever` (in `Api/Abstractions/`), not on `KnowledgeService` or `IMemoryService` directly. To change retrieval strategy:
1. Create a new class implementing `IMemoryRetriever`
2. Change the DI registration in `Program.cs`

```csharp
// Current: FreeLLMApi (OpenAI-compatible)
builder.Services.AddHttpClient<FreeLlmApiClient>();
builder.Services.AddSingleton<IModelGateway>(sp => sp.GetRequiredService<FreeLlmApiClient>());

// Future: alternative provider
builder.Services.AddSingleton<IModelGateway, AlternativeModelGateway>();
```

### Target Architecture (Partially Implemented)

```
External Clients
        │
        ▼
API / Gateway Layer (Controller)
        │
        ├── Mode Detection / Model Selection
        │
        ▼
IPromptIntelligenceEngine ✅ (Phase 7)
        │
        ├── IMemoryRetriever ✅ (Phase 5)
        │     └── Keyword retrieval
        │     └── Future: vector/hybrid retrieval
        ├── Profile Loading
        └── Prompt Assembly
        │
        ▼
IModelGateway ✅ (Phase 4)
        │
        ├── FreeLlmApiClient (current)
        └── Future: alternative providers
        │
        ▼
Downstream Model / Agent Runtime / Tools
```

See [PROJECT_VISION.md](PROJECT_VISION.md) for the full vision.

---

## Domain Model

### MemoryEntry

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Title` | `string` | Short descriptive title (max 500) |
| `Content` | `string` | Full memory content |
| `Scope` | `MemoryScope` | Where it applies (Global, Project, Workspace, Private) |
| `State` | `MemoryState` | Lifecycle state (Active, Updated, Superseded, Expired, Archived, Deleted) |
| `Classification` | `DataClassification` | Sensitivity (Public, Internal, Confidential, Secret) |
| `ProjectId` | `Guid?` | Associated project (null for global memories) |
| `Source` | `string?` | Provenance/origin |
| `TagsJson` | `string?` | JSON-serialized tag list |
| `SupersededById` | `Guid?` | ID of the memory that superseded this one |
| `CreatedAt` | `DateTime` | Creation timestamp |
| `UpdatedAt` | `DateTime` | Last update timestamp |
| `ExpiresAt` | `DateTime?` | Optional expiration |
| `Importance` | `double` | 0.0–1.0 importance score (default 0.5) |
| `MetadataJson` | `string?` | Extensible metadata |

**Domain methods:** `SetTags()`, `Supersede(id)`, `Expire()`, `Archive()`, `SoftDelete()`
**Computed properties:** `IsExpired`, `IsActive`

### Project

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Name` | `string` | Unique name (max 200) |
| `Description` | `string?` | Optional description (max 2000) |
| `ConfigurationJson` | `string?` | Extensible configuration |
| `CreatedAt` | `DateTime` | Creation timestamp |
| `UpdatedAt` | `DateTime` | Last update timestamp |
| `Memories` | `ICollection<MemoryEntry>` | Navigation property |

---

## API Endpoints

### Memory Management — `MemoryController` (`/api/Memory`)

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/Memory` | Create memory entry |
| `GET` | `/api/Memory/{id}` | Get by ID |
| `GET` | `/api/Memory` | Search/list (query, scope, projectId, tags) |
| `PUT` | `/api/Memory/{id}` | Update fields |
| `DELETE` | `/api/Memory/{id}` | Soft-delete (sets state to Deleted) |
| `POST` | `/api/Memory/{id}/supersede` | Create replacement and mark old as superseded |
| `POST` | `/api/Memory/expire` | Process all expired entries |
| `GET` | `/api/Memory/stats` | Statistics by scope and state |

### Projects — `ProjectsController` (`/api/Projects`)

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/Projects` | Create project |
| `GET` | `/api/Projects` | List all |
| `GET` | `/api/Projects/{id}` | Get by ID |
| `PUT` | `/api/Projects/{id}` | Update |
| `DELETE` | `/api/Projects/{id}` | Delete |

### OpenAI-Compatible Gateway — `OpenAIChatCompletionController` (`/v1`)

| Method | Path | Description |
|---|---|---|
| `POST` | `/v1/chat/completions` | Enriched chat completion (streaming or non-streaming) |
| `GET` | `/v1/models` | List models from upstream provider |
| `GET` | `/v1/models/{modelId}` | Get model details |

**Chat completion processing:**
1. Validate request
2. Detect mode → select model
3. Prepare enriched prompt via IPromptIntelligenceEngine (profiles + retrieval + assembly)
4. Forward to downstream provider
5. Stream or return response with token metrics

### Knowledge — `KnowledgeController` (`/api/Knowledge`)

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/Knowledge` | Search documents (query, project, tags) |
| `GET` | `/api/Knowledge/documents` | List all |
| `GET` | `/api/Knowledge/{id}` | Get by ID |
| `POST` | `/api/Knowledge` | Create (title, content, project, tags) |
| `POST` | `/api/Knowledge/reindex` | Reload all documents |

### Profiles — `ProfilesController` (`/api/Profiles`)

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/Profiles` | List loaded profiles |
| `POST` | `/api/Profiles` | Load profile from file path |

### Health

| Method | Path | Description |
|---|---|---|
| `GET` | `/health` | Database connectivity check |

---

## Dependency Injection

| Registration | Type | Lifetime |
|---|---|---|
| `IMemoryRepository → MemoryRepository` | Scoped | EF Core DbContext |
| `IProjectRepository → ProjectRepository` | Scoped | EF Core DbContext |
| `IMemoryService → MemoryService` | Scoped | Application service |
| `IProjectService → ProjectService` | Scoped | Application service |
| `ProfileService` | Singleton | File-based, in-memory cache |
| `KnowledgeService` | Singleton | File-based, in-memory index |
| `PromptBuilder` | Singleton | Stateless prompt construction |
| `IMemoryRetriever → ContextRetrievalService` | Singleton | Orchestrates memory + knowledge retrieval |
| `IPromptIntelligenceEngine → PromptIntelligenceService` | Singleton | Orchestrates context retrieval + prompt assembly |
| `IModelGateway → FreeLlmApiClient` | Transient | HTTP client for providers |
| `FreeLlmApiClient` | Transient (HttpClient) | Concrete provider adapter |
| `RequestLogger` | Singleton | Stateless logging |
| `AppSettings` | Options | Bound from `appsettings.json` |
| `ModelSelectionSettings` | Options | Bound from `AppSettings:ModelSelection` |
| `DeveloperMemoryDbContext` | Scoped | PostgreSQL or InMemory |

---

## Configuration

### appsettings.json

```json
{
  "UseInMemoryDatabase": false,
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=developermemory;Username=developer;Password=devpassword"
  },
  "AppSettings": {
    "FreeLlmApi": {
      "BaseUrl": "http://localhost:3001/v1",
      "ApiKey": "",
      "DefaultModel": "auto"
    },
    "ModelSelection": {
      "AutoSelectModel": true,
      "PlanModel": "auto:smart",
      "BuildModel": "auto:fast"
    },
    "Paths": {
      "KnowledgeFolder": "./Knowledge",
      "ProfilesFolder": "./Profiles",
      "RequestLogFolder": "./logs/requests"
    }
  }
}
```

### Environment Variable Overrides

Use `__` separator:
- `AppSettings__FreeLlmApi__BaseUrl`
- `AppSettings__FreeLlmApi__ApiKey`
- `AppSettings__FreeLlmApi__DefaultModel`

### Model Resolution Priority

1. Per-request `model` field (highest)
2. `AppSettings:FreeLlmApi:DefaultModel`
3. `"auto"` fallback

---

## Docker

### Dockerfile

Multi-stage build:
- **Stage 1 (build):** `mcr.microsoft.com/dotnet/sdk:10.0` — restore, publish
- **Stage 2 (runtime):** `mcr.microsoft.com/dotnet/aspnet:10.0` — published output, profiles, knowledge

### docker-compose.yml

| Service | Image | Purpose |
|---|---|---|
| `api` | Built from Dockerfile | In-memory mode, development |
| `api-postgres` | Built from Dockerfile | PostgreSQL mode, production-like |
| `postgres` | `pgvector/pgvector:pg16` | PostgreSQL with pgvector extension |
| `redis` | `redis:7-alpine` | Cache (available, not yet integrated) |

```bash
# In-memory mode
docker compose up api

# PostgreSQL mode
docker compose up api-postgres
```

---

## Testing

### Test Projects

```
tests/
├── DeveloperMemory.Domain.Tests/
│   └── MemoryEntryTests.cs              # 12 methods: lifecycle, scopes, states, tags
├── DeveloperMemory.Application.Tests/
│   └── MemoryServiceTests.cs            # 16 methods: CRUD, validation, supersession
├── DeveloperMemory.Infrastructure.Tests/
│   ├── InMemoryDbFixture.cs              # Shared EF Core InMemory fixture
│   ├── MemoryRepositoryTests.cs          # 16 methods: CRUD, search, filtering
│   └── ProjectRepositoryTests.cs         # 7 methods: CRUD, list
└── DeveloperMemory.Api.Tests/
    ├── IModelGatewayTests.cs             # 15 methods: gateway abstraction, contract
    ├── IMemoryRetrieverTests.cs          # 10 methods: retrieval abstraction, contract
    └── IPromptIntelligenceEngineTests.cs # 14 methods: engine abstraction, contract
```

**Total: ~90 test methods** across 4 projects.

**Framework:** xUnit 2.9.3 with EF Core InMemory 10.0.0

### Run Tests

```bash
# All tests
dotnet test

# Specific project
dotnet test tests/DeveloperMemory.Infrastructure.Tests/
```

---

## Coding Standards

- **PascalCase** for classes, methods, properties
- **camelCase** for locals, parameters
- **`_camelCase`** for private fields
- File-scoped namespaces
- Nullable reference types enabled
- Pass `CancellationToken` through async chains
- Controllers delegate to services (thin controllers)
- One class per file (exception: OpenAI types grouped in `OpenAIRequestResponse.cs`)

---

## Key Rules for AI Agents

1. **Source code is the authority** for current implementation. Documentation may lag.
2. **Do not collapse the project** back into a simple RAG gateway. The Clean Architecture structure is intentional.
3. **Do not tightly couple** core logic to FreeLlmApiClient or one provider. Keep providers replaceable.
4. **Do not place all new business logic** in the API project. Use Application layer for use cases, Domain for business rules.
5. **Preserve Clean Architecture boundaries.** Domain → Application → Infrastructure → API.
6. **Keep retrieval replaceable.** Use interfaces for any new retrieval strategy.
7. **Treat Prompt Intelligence** as a core architectural capability, not a convenience feature.
8. **Do not implement blind memory capture.** Selective and controlled capture is a design requirement.
9. **Distinguish implemented features from planned architecture.** Source code = current. Vision doc = target.
10. **Prefer incremental refactoring** over unnecessary rewrites.
11. **Do not introduce heavyweight infrastructure** without a concrete need.
12. **Maintain cloud-first suitability** while keeping local development working.
13. **Preserve compatibility** with free/self-hosted alternatives where practical.

---

## Dependencies

### Source Projects

| Package | Version | Project | Purpose |
|---|---|---|---|
| `Microsoft.EntityFrameworkCore` | 10.0.0 | Infrastructure | ORM |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.0 | Infrastructure + Api | Migration tooling |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.0 | Infrastructure | PostgreSQL provider |
| `Microsoft.EntityFrameworkCore.InMemory` | 10.0.0 | Infrastructure + Tests | In-memory DB |
| `Microsoft.Extensions.Configuration.Binder` | 10.0.0 | Infrastructure | Config binding |
| `Serilog.AspNetCore` | 8.0.3 | Api | Structured logging |
| `Serilog.Sinks.Console` | 6.0.0 | Api | Console output |
| `Serilog.Sinks.File` | 6.0.0 | Api | File output |
| `Microsoft.AspNetCore.OpenApi` | 10.0.10 | Api | OpenAPI spec |
| `Swashbuckle.AspNetCore` | 10.0.1 | Api | Swagger UI |
| `OpenTelemetry.*` | 1.18.0 | Api | Observability |

### Test Projects

| Package | Version | Purpose |
|---|---|---|
| `xunit` | 2.9.3 | Test framework |
| `Microsoft.NET.Test.Sdk` | 17.13.0 | Test runner |
| `xunit.runner.visualstudio` | 2.8.2 | VS integration |
| `coverlet.collector` | 6.0.4 | Code coverage |
| `Microsoft.EntityFrameworkCore.InMemory` | 10.0.0 | In-memory DB for tests |

---

## Related Documentation

- [PROJECT_VISION.md](PROJECT_VISION.md) — Canonical vision, principles, target architecture
- [CURRENT_STATUS.md](CURRENT_STATUS.md) — Verified implementation inventory
- [ROADMAP.md](ROADMAP.md) — Development roadmap
- [AGENTS.md](AGENTS.md) — AI agent coding guide
- [KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md) — Frontmatter format reference
- [docs/ARCHITECTURE_AUDIT.md](docs/ARCHITECTURE_AUDIT.md) — Architecture audit and gap analysis
