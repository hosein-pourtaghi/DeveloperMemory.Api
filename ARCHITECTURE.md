# DeveloperMemory.Api Architecture

**Last updated:** 2026-08-28

This document separates the verified implementation from the longer-term product direction. Source code remains authoritative for current behavior.

## Current Implementation

DeveloperMemory.Api uses Clean Architecture:

```text
Domain ← Application ← Infrastructure ← API
```

- **Domain:** entities, lifecycle/state rules, enums, and repository interfaces.
- **Application:** use cases, contracts, deterministic Prompt Intelligence, retrieval orchestration, ownership-aware prompt history, and policy boundaries.
- **Infrastructure:** EF Core `DeveloperMemoryDbContext`, PostgreSQL/InMemory provider selection, repositories, migrations, embedding/vector foundations, and service registration.
- **API:** ASP.NET Core controllers, authentication, Kestrel composition, OpenAI-compatible gateway, file-based knowledge/profile services, and HTTP middleware.

### Verified Prompt History Flow

```text
PromptIntelligenceController
        ↓
IPromptProcessingHistoryService
        ↓
PromptProcessingHistoryService
        ↓
IPromptProcessingRecordRepository
        ↓
PromptProcessingRecordRepository
        ↓
DeveloperMemoryDbContext
        ↓
PostgreSQL
```

Both Phase K service/repository mappings are scoped and owner-aware. History queries preserve profile, date, optimization, validation, fallback, and bounded-result behavior.

### Current Retrieval Flow

```text
Gateway / Prompt Intelligence
        ↓
IMemoryRetrievalService
        ↓
Keyword retrieval by default
        ├── owner/project/workspace/private filtering
        ├── lifecycle and expiration filtering
        ├── deterministic ranking
        └── bounded results
```

Semantic/vector and hybrid foundations are present and selectable when configured, but the default local configuration remains keyword-only because no external embedding provider is enabled.

### Current Prompt Intelligence Flow

```text
Request
  ↓
Intent/task analysis
  ↓
Memory and project context retrieval
  ↓
Constraint resolution and context assembly
  ↓
Deterministic prompt construction and optimization
  ↓
Optional downstream model gateway
```

This deterministic pipeline is implemented and tested. Advanced LLM-assisted semantic interpretation is not complete.

## Target Architecture

The long-term control-plane direction is:

```text
User
 ↓
Personal AI Assistant
 ↓
DeveloperMemory.Api
 ↓
Central Orchestrator
 ├── Memory
 ├── Models
 ├── Agents
 ├── Tools
 └── Workflows
```

The target remains modular and provider-independent:

```text
IAgentRuntime
 ├── FreeBuffAdapter
 ├── OpenHandsAdapter
 └── FutureAgentAdapter

IToolProvider
IMcpClient
IToolRegistry
IToolExecutor
```

These target abstractions are not implemented and must not be introduced prematurely during Phase L.

## Future Capability Boundaries

### Semantic Memory Retrieval — Phase L

Embedding providers and vector stores belong behind replaceable Infrastructure implementations. Application retrieval contracts own filtering, ranking, lifecycle, and budgeting decisions. API controllers must not depend on a concrete embedding or vector provider.

### Memory Intelligence — Phase M

Selective capture, importance/confidence/relevance evaluation, duplicate and contradiction analysis, consolidation, and intelligent lifecycle decisions belong in Domain/Application policy boundaries. Capture must never become blind transcript storage.

### Advanced LLM Prompt Intelligence — Phase N

LLM-assisted analysis and optimization must extend, not remove, deterministic fallback and safety controls. Provider-specific calls remain replaceable and bounded.

### Project and Workspace Context — Phase O

`IProjectContextProvider` and repository/source/documentation context providers should remain replaceable external integrations. Project, workspace, ownership, and task boundaries must be explicit.

### Agent Runtime — Phase P

Agent adapters belong outside core domain rules. The API remains an intelligence/control plane rather than a monolithic agent runtime.

### MCP and Tools — Phase Q

MCP and tool discovery/execution must use explicit permission, timeout, audit, and provider boundaries. No unrestricted invocation is implied.

### Central Orchestration — Phase R

The orchestrator composes existing memory, model, agent, tool, and workflow contracts. It must preserve authorization, lifecycle, provider, and budget boundaries.

### Production Operations — Phase S

Deployment, secret management, resilience, scaling, backup/recovery, and operational security are deployment/operations concerns, not Domain business logic. Local PostgreSQL and Kestrel development remains supported.

### Advanced Interfaces — Phase T

Voice, scheduling, external actions, email, and autonomous workflows are long-term interface/workflow concerns that consume the central orchestration contracts.

## Non-Goals

- Do not collapse the four-project architecture.
- Do not couple core behavior to one LLM, embedding, vector, agent, MCP, or cloud provider.
- Do not implement blind memory capture.
- Do not turn DeveloperMemory.Api into a full autonomous agent.
- Do not treat deterministic foundations as proof that future semantic, orchestration, or production capabilities are complete.

See [PROJECT_VISION.md](PROJECT_VISION.md) for product direction and [ROADMAP.md](ROADMAP.md) for phase objectives and acceptance criteria.
