# DeveloperMemory.Api

A **persistent, intelligent AI memory layer and Memory Intelligence Gateway**.

DeveloperMemory.Api sits between AI systems and LLM providers. It maintains persistent, structured, lifecycle-managed memory that helps AI coding assistants, agents, and tools access relevant context instead of starting from zero in every interaction.

---

## What It Does

**The problem:** AI models are stateless. They forget your preferences, ignore project context, and require you to re-explain everything in every conversation.

**The solution:** DeveloperMemory.Api remembers relevant information once, retrieves it only when relevant, and provides it to AI systems at the right time — automatically.

## How It Works

```
AI Application / Agent
        │
        ▼
DeveloperMemory.Api  (Memory Intelligence Gateway)
        │
        ├── Load Developer Profile(s)
        ├── Search Knowledge Documents (keyword-based)
        ├── Retrieve Persistent Memory (PostgreSQL)
        ├── Assemble Context & Enrich System Message
        ├── Mode Detection (Plan vs Build) → Auto Model Selection
        │
        ▼
Forward to LLM Provider (streaming or non-streaming)
        │
        ▼
Response back to AI Application
```

---

## Current Capabilities

Phase K is complete and verified. Phase L — Semantic Memory Retrieval — is the next implementation phase. See [CURRENT_STATUS.md](CURRENT_STATUS.md) and [ROADMAP.md](ROADMAP.md) for the verified inventory and remaining work.

### Persistent Memory System (Database-backed)
- ✅ Full CRUD for memory entries with PostgreSQL persistence
- ✅ Memory lifecycle: Active, Updated, Superseded, Expired, Archived, Deleted
- ✅ Memory scopes: Global, Project, Workspace, Private
- ✅ Data classification: Public, Internal, Confidential, Secret
- ✅ Importance scoring for retrieval ranking
- ✅ Tag-based filtering
- ✅ Project-scoped memory
- ✅ Manual supersession (create replacement, mark old as superseded)
- ✅ Automatic expiration processing
- ✅ Soft deletion
- ✅ Statistics endpoint (counts by scope and state)

### Project Management
- ✅ Create, read, update, delete projects
- ✅ Project-scoped memory association
- ✅ Memory count per project

### OpenAI-Compatible Gateway
- ✅ `/v1/chat/completions` with full streaming (SSE) support
- ✅ Context enrichment from profiles, knowledge, AND persistent memory
- ✅ Mode detection (plan vs build) with automatic model selection
- ✅ Token tracking and three-stage logging
- ✅ OpenAI-compatible error responses
- ✅ `/v1/models` and `/v1/models/{id}` endpoints
- ✅ Multimodal content forwarding

### Legacy Knowledge & Profile System
- ✅ Markdown knowledge documents with YAML frontmatter
- ✅ Keyword-based relevance search
- ✅ Developer profile loading from Markdown files
- ✅ Knowledge creation API

### Infrastructure
- ✅ Clean Architecture (Domain → Application → Infrastructure → API)
- ✅ Entity Framework Core with PostgreSQL
- ✅ EF Core migrations
- ✅ Four active test projects with 634 verified tests (xUnit plus native PostgreSQL integration coverage)
- ✅ Health check endpoint
- ✅ OpenTelemetry integration (configurable)
- ✅ Swagger/OpenAPI documentation
- ✅ Serilog structured logging
- ✅ Docker deployment (Dockerfile + docker-compose.yml)
- ✅ PostgreSQL with pgvector extension (for future vector search)

### Partial or Future Capabilities
- Semantic/vector retrieval: foundations exist, but the default local runtime remains keyword-only without an enabled external embedding provider
- Intelligent/selective memory capture and lifecycle decisioning
- Advanced LLM-powered Prompt Intelligence
- Complete project/workspace/repository context intelligence
- Agent runtime abstraction and execution integration
- MCP and tool integration
- Central AI orchestration / personal AI control plane
- Production/cloud deployment and operational hardening
- Voice, scheduled, external-service, and advanced workflow automation

Authentication, ownership, PostgreSQL persistence, and multi-user isolation are implemented and verified. See [CURRENT_STATUS.md](CURRENT_STATUS.md) for the full implementation inventory.

See [CURRENT_STATUS.md](CURRENT_STATUS.md) for the full implementation inventory.

---

## Architecture

```
DeveloperMemory.Api.sln (at repository root)
│
├── src/
│   ├── DeveloperMemory.Domain/           # Entities, enums, repository interfaces
│   ├── DeveloperMemory.Application/      # Use cases, contracts, DTOs
│   ├── DeveloperMemory.Infrastructure/   # EF Core, persistence, DI
│   └── DeveloperMemory.Api/              # Controllers, services, gateway
│
├── tests/
│   ├── DeveloperMemory.Domain.Tests/        # Entity lifecycle tests (12 methods)
│   ├── DeveloperMemory.Application.Tests/   # Service logic tests (16 methods)
│   └── DeveloperMemory.Infrastructure.Tests/ # Repository tests (23 methods)
│
├── Dockerfile
├── docker-compose.yml
└── .dockerignore
```

**Dependency direction:** Domain ← Application ← Infrastructure ← API

See [CLAUDE.md](CLAUDE.md) for the complete technical reference, [PROJECT_VISION.md](PROJECT_VISION.md) for the product vision, [ARCHITECTURE.md](ARCHITECTURE.md) for current/target boundaries, and [ROADMAP.md](ROADMAP.md) for Phase L onward.

---

## Quick Start

### Local development (native PostgreSQL + Kestrel)

```bash
dotnet restore
dotnet run --project src/DeveloperMemory.Api
```

### Optional container deployment artifacts

Dockerfile and docker-compose.yml are retained for deployment scenarios, but Docker is not required for local development or Phase K verification.

### Without Docker

```bash
dotnet restore
dotnet run --project src/DeveloperMemory.Api
```

- **API**: `http://localhost:5041`
- **Swagger UI**: `/swagger` (Development mode)
- **Health Check**: `GET /health`

### Requirements

- .NET 10.0 SDK (or Docker)
- PostgreSQL (or set `"UseInMemoryDatabase": true` — default in Docker)

---

## Running Tests

```bash
dotnet test
```

Or run specific test projects:

```bash
dotnet test tests/DeveloperMemory.Domain.Tests/
dotnet test tests/DeveloperMemory.Application.Tests/
dotnet test tests/DeveloperMemory.Infrastructure.Tests/
```

---

## Configuration

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
    }
  }
}
```

Environment variables: `AppSettings__FreeLlmApi__BaseUrl`, `AppSettings__FreeLlmApi__ApiKey`, etc.

---

## API Endpoints

### Memory Management (`/api/Memory`)
| Method | Path | Description |
|---|---|---|
| `POST` | `/api/Memory` | Create a memory entry |
| `GET` | `/api/Memory/{id}` | Get by ID |
| `GET` | `/api/Memory` | Search or list (query, scope, projectId, tags) |
| `PUT` | `/api/Memory/{id}` | Update a memory entry |
| `DELETE` | `/api/Memory/{id}` | Soft-delete |
| `POST` | `/api/Memory/{id}/supersede` | Supersede with replacement |
| `POST` | `/api/Memory/expire` | Process expired entries |
| `GET` | `/api/Memory/stats` | Statistics by scope and state |

### Projects (`/api/Projects`)
| Method | Path | Description |
|---|---|---|
| `POST` | `/api/Projects` | Create a project |
| `GET` | `/api/Projects` | List all projects |
| `GET` | `/api/Projects/{id}` | Get by ID |
| `PUT` | `/api/Projects/{id}` | Update |
| `DELETE` | `/api/Projects/{id}` | Delete |

### OpenAI-Compatible Gateway (`/v1`)
| Method | Path | Description |
|---|---|---|
| `POST` | `/v1/chat/completions` | Enriched chat completion |
| `GET` | `/v1/models` | List models |
| `GET` | `/v1/models/{modelId}` | Get model details |

### Legacy Knowledge & Profiles (`/api`)
| Method | Path | Description |
|---|---|---|
| `GET` | `/api/Knowledge` | Search knowledge documents |
| `GET` | `/api/Knowledge/documents` | List all documents |
| `POST` | `/api/Knowledge` | Create document |
| `POST` | `/api/Knowledge/reindex` | Reload documents |
| `GET` | `/api/Profiles` | List profiles |
| `POST` | `/api/Profiles` | Load profile from file |

---

## Documentation

| Document | Purpose |
|---|---|
| [PROJECT_VISION.md](PROJECT_VISION.md) | Canonical vision, principles, and target architecture |
| [CURRENT_STATUS.md](CURRENT_STATUS.md) | Verified implementation inventory |
| [ROADMAP.md](ROADMAP.md) | Development roadmap |
| [CLAUDE.md](CLAUDE.md) | Complete technical reference |
| [AGENTS.md](AGENTS.md) | AI agent coding guide |
| [KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md) | Knowledge and profile format reference |
| [DOCUMENTATION.md](DOCUMENTATION.md) | Documentation index |
| [CHANGELOG.md](CHANGELOG.md) | Version history |
| [docs/ARCHITECTURE_AUDIT.md](docs/ARCHITECTURE_AUDIT.md) | Architecture audit and gap analysis |

---

## License

Internal project — see repository for license details.
