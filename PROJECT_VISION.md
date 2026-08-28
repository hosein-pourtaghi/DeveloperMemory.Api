# PROJECT_VISION.md — DeveloperMemory.Api

*Last updated: 2026-08-25*

---

## 1. Project Identity

**DeveloperMemory.Api** is a **persistent, intelligent AI memory layer and Memory Intelligence Gateway**.

It sits between AI systems (coding assistants, agents, IDE tools, CLI tools, and other AI clients) and LLM/model providers. Its purpose is to provide persistent, relevant, lifecycle-managed context that helps AI systems deliver better, more informed responses.

It is **not** merely:
- A simple RAG application
- A developer knowledge gateway
- A Markdown knowledge search API
- A basic OpenAI proxy
- A simple prompt builder
- An autonomous AI agent
- A replacement for an LLM
- A system that blindly stores every conversation

---

## 2. Core Problem

AI systems are typically stateless or have fragmented context. Important information becomes scattered across:

- Conversations (lost between sessions)
- IDE sessions (tied to one editor instance)
- Projects (not shared across tools)
- Agents and tools (each with their own context)
- Files (scattered across directories)
- Prompts (manually re-injected each time)

This forces developers to re-explain context repeatedly, leads to inconsistent AI behavior, and prevents knowledge from accumulating over time.

**DeveloperMemory.Api solves this** by providing a centralized, persistent, intelligent memory and context layer that AI systems can query for relevant information at the right time.

---

## 3. Where the Project Came From

The project originated as a developer-focused persistent context and knowledge gateway — a way to store Markdown-based knowledge documents and developer profiles, then inject relevant context into AI requests via an OpenAI-compatible proxy. This V1 approach is still functional and part of the current system.

## 4. Where the Project Is Now

The project has evolved into a **Clean Architecture-based system** with:

- **Four-layer architecture**: Domain, Application, Infrastructure, API
- **Persistent database storage**: PostgreSQL via Entity Framework Core, with EF Core migrations
- **Memory domain model**: `MemoryEntry` with lifecycle states, scopes, classifications, importance, tags, project association, and supersession
- **Project domain model**: Scoping memories to specific projects
- **Full memory management APIs**: Create, read, update, soft-delete, supersede, expire, search, and statistics
- **Legacy knowledge and profile systems**: File-based Markdown knowledge and developer profiles still functional
- **OpenAI-compatible gateway**: Streaming and non-streaming forwarding with context enrichment
- **Mode detection and model selection**: Heuristic plan/build detection with automatic model routing
- **Token tracking and request logging**: Three-stage token estimation with daily file logging
- **Observability foundation**: OpenTelemetry integration (configurable)
- **Test infrastructure**: xUnit tests for repository layer using EF Core InMemory

## 5. Where the Project Is Going

DeveloperMemory.Api is evolving toward a **modular, cloud-first, provider-independent Persistent Intelligent AI Memory Layer** with:

- **Lifecycle-managed memory** with intelligent supersession and expiration
- **Selective and controlled memory capture** (not blind auto-storage)
- **Intelligent context retrieval** — semantic, hybrid, lifecycle-aware
- **A core Prompt Intelligence Engine** for sophisticated request analysis and context preparation
- **Replaceable provider integrations** — LLM providers, vector stores, embedding models, agent runtimes
- **MCP and tooling integration** with proper modular boundaries
- **Cloud-first deployment** with container support and observability
- **Compatibility with free and self-hosted alternatives**

---

## 6. Core Concepts

### What "Memory" Means

In this project, "memory" refers to persistent, structured, lifecycle-managed information that helps AI systems provide better responses. Memory includes:

- User preferences and working conventions
- Explicit instructions and constraints
- Project context and technical decisions
- Developer knowledge and historical outcomes
- Relevant contextual information

Memory is **not** a complete transcript of every interaction. It is a curated, relevant, structured knowledge layer.

### Memory Dimensions

The architecture organizes memory across multiple meaningful dimensions:

| Dimension | Purpose |
|---|---|
| **Scope** | Where the memory applies: Global, Project, Workspace, Private |
| **State/Lifecycle** | Current status: Active, Updated, Superseded, Expired, Archived, Deleted |
| **Classification** | Sensitivity level: Public, Internal, Confidential, Secret |
| **Importance** | Relative significance (0.0–1.0) for retrieval ranking |
| **Tags** | Freeform categorization for filtering |
| **Project** | Association with a specific project for scoped retrieval |
| **Source/Provenance** | Where the memory originated |
| **Temporal validity** | Expiration dates for time-sensitive information |

### Memory Lifecycle States

| State | Description |
|---|---|
| **Active** | Currently valid and available for retrieval |
| **Updated** | Has been modified (previous version may be superseded) |
| **Superseded** | Replaced by newer, more accurate information |
| **Expired** | Past its expiration date |
| **Archived** | No longer active but preserved for historical reference |
| **Deleted** | Soft-deleted, no longer returned in queries |

### Memory Scopes

| Scope | Description |
|---|---|
| **Global** | Applies across all projects and contexts |
| **Project** | Associated with a specific project |
| **Workspace** | Applicable within a workspace context |
| **Private** | Restricted to specific access |

---

## 7. Selective Memory Capture Principle

DeveloperMemory.Api is intentionally **not** a system that blindly saves every interaction. The target architecture requires that automatic memory capture be:

- **Selective** — only valuable information is captured
- **Controlled** — configurable policies govern what is stored
- **Explainable** — when possible, the system can explain why something was captured
- **Privacy-aware** — respects classification and access boundaries
- **Bounded** — constrained by explicit policies, scopes, and user intent

The intended future flow:

```
Interaction / Input
        │
        ▼
Candidate Memory Extraction
        │
        ▼
Value / Importance Evaluation
        │
        ▼
Classification and Context Assignment
        │
        ▼
Duplicate / Similarity Analysis
        │
        ▼
Contradiction Analysis
        │
        ▼
Decision: Create / Update / Supersede / Ignore
        │
        ▼
Lifecycle-managed Persistent Memory
```

**Current status**: Explicit manual memory creation via API. No automatic extraction or evaluation is implemented. This is a planned future capability.

---

## 8. Memory Retrieval Vision

Retrieval should not be "search all and return everything." The target retrieval system should intelligently determine:

- What the user or AI system is trying to accomplish
- What context is required
- Which memory scopes are relevant
- Which project context applies
- Which instructions or constraints are active
- Which memories are current versus superseded or expired
- How much context fits in the available budget
- Which information is most relevant and important

The intended retrieval evolution:

```
Current:  Keyword-based text search (implemented)
Target:   Semantic retrieval, vector search, hybrid search,
          metadata/project/scope filtering, lifecycle-aware
          filtering, importance-aware ranking
```

---

## 9. Prompt Intelligence Engine

The Prompt Intelligence Engine is a **core architectural capability**, not a convenience feature. Its role is to analyze a request before execution and prepare an execution-ready context/prompt package.

Target conceptual flow:

```
Request / Interaction
        │
        ▼
Intent and Task Analysis
        │
        ▼
Prompt Intelligence Engine
        ├── Determine objective
        ├── Determine task characteristics
        ├── Determine required context
        ├── Determine relevant memory scopes
        ├── Resolve relevant project context
        ├── Retrieve and rank relevant memories
        ├── Apply project rules
        ├── Apply user instructions and constraints
        ├── Detect or surface conflicts
        ├── Manage context/token budget
        ├── Structure and optimize the prompt
        ├── Determine execution requirements
        └── Optionally support human review
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
Optional Memory Capture Pipeline
```

**Current status**: The system has building blocks — `PromptBuilder` (context assembly), `ModeDetector` (heuristic mode detection), `KnowledgeService` (file-based knowledge), `ProfileService` (file-based profiles), `MemoryService` (persistent memory retrieval), and request enrichment in the OpenAI controller. These are foundational components, not the complete Prompt Intelligence Engine. The full engine is a planned architectural evolution.

---

## 10. Modular and Replaceable Architecture

This is a fixed architectural principle. DeveloperMemory.Api must be modular and implementation/provider agnostic. The following capabilities should be behind replaceable abstractions:

| Capability | Current Implementation | Target Abstraction |
|---|---|---|
| Model/LLM Providers | FreeLlmApiClient (OpenAI-compatible) | IModelGateway |
| Memory Persistence | PostgreSQL + EF Core | IMemoryRepository (exists) |
| Retrieval | Keyword search (EF Core Contains) | IMemoryRetriever |
| Memory Evaluation | None (manual creation) | IMemoryIntelligenceService |
| Prompt Intelligence | PromptBuilder (basic enrichment) | IPromptIntelligenceEngine |
| Project Context | ProjectService + MemoryService | IProjectContextProvider |
| Agent Runtimes | None (gateway only) | IAgentRuntime |
| MCP/Tools | None (planned) | MCP-related abstractions |
| Embeddings/Vector | None (planned) | IEmbeddingProvider |

The system must not become fundamentally coupled to one LLM vendor, vector database, embedding model, agent framework, or cloud provider.

---

## 11. Cloud-First Principle

DeveloperMemory.Api is designed as **cloud-first** rather than local-first. The current infrastructure supports container-friendly deployment:

**Currently implemented:**
- PostgreSQL persistence (externalized database)
- EF Core with migrations
- Health check endpoint
- OpenTelemetry integration (configurable)
- ASP.NET Core with Kestrel

**Target cloud architecture:**
- Container deployment (Docker/OCI)
- Externalized configuration via environment variables
- Managed or self-hosted persistent storage
- Observability (traces, metrics, logs)
- Secure secret handling
- Scalability where required

The project remains compatible with local development and self-hosted deployment. Cloud-first does not mean cloud-only.

---

## 12. Agent Runtime and MCP Vision

DeveloperMemory.Api itself is not required to become a full autonomous agent. Instead, it provides intelligent memory, context, and prompt preparation that can be consumed by agents.

```
DeveloperMemory.Api (Memory / Prompt Intelligence)
        │
        ▼
Execution Requirements
        │
        ├── Model
        ├── Agent Runtime
        ├── MCP Servers
        ├── Tools
        └── Other Capabilities
```

Agent runtime implementations should remain replaceable. MCP/tool implementations must have a proper modular boundary and not be deeply embedded into core logic. This is planned architectural direction — no MCP implementation currently exists.

---

## 13. Provider Independence

The project should remain compatible with free and low-cost alternatives. It may use:

- FreeLLM API or other OpenAI-compatible routers
- Direct OpenAI, Anthropic, or other provider APIs
- Local/self-hosted models
- Other free providers
- Future providers

No specific provider should become inseparable from the architecture. The current `FreeLlmApiClient` is the default integration, not the permanent identity.

---

## 14. Explicit Non-Goals

DeveloperMemory.Api is **not**:

- **An LLM** — It does not host or train models
- **A full autonomous agent** — It provides context and memory, not autonomous execution
- **An automated coding system** — It enriches requests; execution happens downstream
- **A system that stores everything indiscriminately** — Memory capture must be selective
- **Permanently tied to one provider** — The architecture is provider-agnostic
- **Merely a vector database** — It is a memory intelligence system, not just a store
- **Merely a RAG wrapper** — It includes lifecycle management, prompt intelligence, and gateway capabilities beyond simple retrieval-augmented generation

---

## 15. Architectural Principles

1. **Source code is the authority** for current implementation status
2. **The canonical vision** determines intended direction
3. **Clean Architecture boundaries** — Domain → Application → Infrastructure → API
4. **Modular and replaceable implementations**
5. **Provider agnostic** — no vendor lock-in
6. **Cloud-first** with local/self-hosted compatibility
7. **Selective memory** — not blind auto-capture
8. **Lifecycle-aware** — memory has states and transitions
9. **Relevance-first retrieval** — not dump-everything injection
10. **Incremental evolution** — prefer refactoring over rewrites
11. **Avoid premature complexity** — add infrastructure only when concretely needed
12. **Preserve replaceability** — abstractions over concrete implementations where it matters

---

## 16. Related Documents

- [README.md](README.md) — Concise overview, current capabilities, quick start
- [CURRENT_STATUS.md](CURRENT_STATUS.md) — Verified implementation inventory
- [ROADMAP.md](ROADMAP.md) — Future evolution phases
- [CLAUDE.md](CLAUDE.md) — Complete technical reference
- [AGENTS.md](AGENTS.md) — AI agent coding guide
- [KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md) — Knowledge and profile format reference
- [docs/ARCHITECTURE_AUDIT.md](docs/ARCHITECTURE_AUDIT.md) — Architecture audit and gap analysis
