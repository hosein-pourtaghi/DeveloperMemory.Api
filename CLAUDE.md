# CLAUDE.md — DeveloperMemory.Api

## Project Overview

DeveloperMemory.Api is a **persistent, intelligent AI memory layer and Memory Intelligence Gateway**. It enables AI systems to remember relevant information across conversations and provide contextually appropriate responses.

**Core purpose:** Prevent developers and AI coding assistants from repeatedly needing to rediscover or manually provide important context.

**Core architecture:** Clean Architecture with 4 source projects + 1 test project, PostgreSQL persistence via EF Core, and an OpenAI-compatible gateway with context enrichment.

**Source code is the authority** for current implementation status. Target architecture is documented in [PROJECT_VISION.md](PROJECT_VISION.md).

---

## Repository Structure

```
src/
├── DeveloperMemory.Domain/           # Entities, enums, repository interfaces
│   ├── Entities/
│   │   ├── BaseEntity.cs
│   │   ├── MemoryEntry.cs
│   │   └── Project.cs
│   ├── Enums/
│   │   ├── DataClassification.cs     # Public, Internal, Confidential, Secret
│   │   ├── MemoryScope.cs            # Global, Project, Workspace, Private
│   │   └── MemoryState.cs            # Active, Updated, Superseded, Expired, Archived, Deleted
│   └── Interfaces/
│       ├── IMemoryRepository.cs
│       └── IProjectRepository.cs
│
├── DeveloperMemory.Application/      # Use cases, contracts, DTOs
│   ├── Contracts/
│   │   ├── IMemoryService.cs
│   │   └── IProjectService.cs
│   ├── Services/
│   │   ├── MemoryService.cs
│   │   └── ProjectService.cs
│   ├── DTOs/
│   │   ├── CreateMemoryRequest.cs
│   │   ├── UpdateMemoryRequest.cs
│   │   ├── MemoryDto.cs
│   │   ├── MemoryStatsDto.cs
│   │   ├── CreateProjectRequest.cs
│   │   ├── UpdateProjectRequest.cs
│   │   └── ProjectDto.cs
│   └── Exceptions/
│       ├── DomainException.cs
│       ├── MemoryNotFoundException.cs
│       └── ProjectNotFoundException.cs
│
├── DeveloperMemory.Infrastructure/   # EF Core, persistence, DI
│   ├── Persistence/
│   │   ├── DeveloperMemoryDbContext.cs
│   │   ├── MemoryRepository.cs
│   │   ├── ProjectRepository.cs
│   │   └── Configurations/
│   │       ├── MemoryEntryConfiguration.cs
│   │       └── ProjectConfiguration.cs
│   ├── Migrations/
│   │   └── 20260824182548_InitialCreate.cs
│   └── DependencyInjection/
│       └── ServiceCollectionExtensions.cs
│
├── DeveloperMemory.Api/              # Controllers, services, gateway
│   ├── Controllers/
│   │   ├── MemoryController.cs
│   │   ├── ProjectsController.cs
│   │   ├── KnowledgeController.cs
│   │   ├── ProfilesController.cs
│   │   └── OpenAIChatCompletionController.cs
│   ├── Services/
│   │   ├── PromptBuilder.cs
│   │   ├── ModeDetector.cs
│   │   ├── KnowledgeService.cs
│   │   ├── ProfileService.cs
│   │   ├── FreeLlmApiClient.cs
│   │   ├── TokenEstimator.cs
│   │   └── RequestLogger.cs
│   ├── Models/                       # OpenAI types, knowledge, profiles
│   ├── Infrastructure/               # Configuration, Middleware
│   ├── Knowledge/                    # Markdown knowledge documents
│   └── Profiles/                     # Markdown developer profiles
│
tests/
└── DeveloperMemory.Infrastructure.Tests/
    ├── InMemoryDbFixture.cs          # Shared EF Core InMemory fixture
    ├── MemoryRepositoryTests.cs      # Memory repository tests
    └── ProjectRepositoryTests.cs     # Project repository tests
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
        ├── Load developer profiles (ProfileService)
        ├── Search knowledge documents (KnowledgeService)
        ├── Retrieve persistent memory (MemoryService.SearchAsync)
        ├── Build enriched request (PromptBuilder)
        │     ├── Append persistent memory context
        │     ├── Append profile context
        │     └── Append knowledge context
        ├── Log token metrics (TokenEstimator + RequestLogger)
        ├── Forward to provider (FreeLlmApiClient)
        │     ├── Non-streaming: await response, serialize
        │     └── Streaming: pipe SSE stream to client
        └── Log response metrics
```

### Target Architecture (Planned)

```
External Clients
        │
        ▼
API / Gateway Layer
        │
        ▼
Application Orchestration
        │
        ▼
Prompt Intelligence Engine
        /       |       \
       /        |        \
      v         v         v
Memory Intelligence  Project Context  Execution Planning
      |                         |
      v                         v
Retrieval / Ranking       Agent / Model / Tools
      |                         |
      v                         v
  Persistence            Provider Adapters
      |
      v
PostgreSQL / Replaceable Stores
```

This target architecture is **not yet implemented**. See [PROJECT_VISION.md](PROJECT_VISION.md) for the full vision.

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
3. Load profiles, search knowledge, retrieve persistent memory
4. Build enriched request (append context to system message)
5. Forward to downstream provider
6. Stream or return response with token metrics

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
| `FreeLlmApiClient` | Transient (HttpClient) | HTTP client for providers |
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

## Testing

### Test Project

```
tests/DeveloperMemory.Infrastructure.Tests/
├── InMemoryDbFixture.cs       # Shared EF Core InMemory context (fresh per test)
├── MemoryRepositoryTests.cs   # 7+ tests: CRUD, search, scope filtering, expiration, soft delete
└── ProjectRepositoryTests.cs  # 7 tests: CRUD, list, delete
```

**Framework:** xUnit 2.9.3 with EF Core InMemory 10.0.0
**Coverage:** Repository layer (MemoryRepository, ProjectRepository)

### Run Tests

```bash
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

### Test Project

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
