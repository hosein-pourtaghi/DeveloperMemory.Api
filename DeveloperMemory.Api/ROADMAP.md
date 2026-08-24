# Roadmap

This roadmap bridges the current implementation (a working LLM proxy with knowledge injection) to the full vision (a Memory Intelligence Gateway).

---

## Phase 0 — Foundation (Completed)

The core infrastructure is implemented and working:

- [x] .NET 10.0 project setup with Serilog, Swagger, CORS
- [x] OpenAI-compatible request/response models with JsonExtensionData forwarding
- [x] `POST /v1/chat/completions` — streaming and non-streaming
- [x] `GET /v1/models` and `GET /v1/models/{modelId}`
- [x] `KnowledgeService` — Markdown parsing, keyword search, document creation
- [x] `ProfileService` — Markdown parsing, profile loading
- [x] `PromptBuilder` — Context enrichment preserving conversation history
- [x] `FreeLlmApiClient` — Provider-agnostic HTTP client with streaming
- [x] `ModeDetector` — Plan vs build mode detection
- [x] `TokenEstimator` — Three-stage token tracking
- [x] `RequestLogger` — Console + daily file logging
- [x] `GlobalExceptionMiddleware` — OpenAI-compatible error responses
- [x] `RequestLoggingMiddleware` — Diagnostic request body logging
- [x] Management API: `GET/POST /api/Knowledge`, `GET /api/Knowledge/{id}`, `POST /api/Knowledge/reindex`
- [x] Management API: `GET/POST /api/Profiles`
- [x] Health check endpoint (`GET /health`)
- [x] Configuration via `appsettings.json` with environment variable overrides

---

## Phase 1 — Quality and Completeness

**Goal:** Make the current implementation production-quality and fill obvious gaps.

**This phase does NOT add new memory capabilities.** It hardens what exists.

### High Priority

- [ ] **Verify build** — Ensure `dotnet build` passes with zero errors
- [ ] **Add unit tests** — Test `KnowledgeService`, `ProfileService`, `PromptBuilder`, `ModeDetector`, `TokenEstimator`
- [ ] **Fix remaining edge cases** — Empty knowledge folder, malformed frontmatter, provider timeout handling
- [ ] **Lock down CORS** — Add configuration option for allowed origins (keep AllowAll for dev)
- [ ] **Remove legacy methods** — Clean up any duplicated functionality in PromptBuilder

### Medium Priority

- [ ] **Add retry logic** — Transient provider failure handling
- [ ] **Improve API documentation** — Ensure Swagger reflects all endpoints correctly
- [ ] **Add structured logging** — Replace string interpolation with structured log properties
- [ ] **Add request validation** — Validate message roles, temperature ranges, etc.

### Low Priority

- [ ] **Add model caching** — Cache upstream model list to avoid repeated calls
- [ ] **Add configuration validation** — Validate settings on startup

---

## Phase 2 — Memory Model Foundation

**Goal:** Establish the core memory data model and storage that will support all future memory capabilities.

**This is the bridge from "knowledge-enriched proxy" to "Memory Intelligence Gateway."**

### Memory Data Model

- [ ] **Define Memory entity** — Core data model with:
  - `Id` (Guid)
  - `Content` (string)
  - `Type` (enum: Preference, Instruction, Constraint, Goal, PersonalFact, ProjectContext, TechnicalKnowledge, Decision, WorkingContext)
  - `State` (enum: Active, Updated, Superseded, Expired, Archived, Deleted)
  - `Scope` (enum: Global, User, Project, Conversation, Session, Agent)
  - `ScopeId` (string — user ID, project name, conversation ID, etc.)
  - `Source` (string — where this memory came from)
  - `Confidence` (double — how confident the system is in this memory)
  - `CreatedAt`, `UpdatedAt`, `SupersededAt`, `ExpiresAt` (DateTimeOffset)
  - `Tags` (List<string>)
  - `Metadata` (Dictionary<string, string>)

- [ ] **Define MemoryEntry entity** — For tracking memory changes over time:
  - `MemoryId` (Guid)
  - `Version` (int)
  - `Content` (string)
  - `State` (MemoryState)
  - `ChangedAt` (DateTimeOffset)
  - `Reason` (string — why this change happened)

### Persistent Storage

- [ ] **Add SQLite database** — Local-first persistent storage
- [ ] **Define EF Core DbContext** — For memory, profile, and knowledge storage
- [ ] **Create migrations** — Database schema versioning
- [ ] **Migrate from file-based to database storage** — Profiles and knowledge stored in DB
- [ ] **Keep file-based as import/export** — Allow loading from Markdown files into DB

### Memory Scope Implementation

- [ ] **Implement User scope** — Memories tied to a specific user ID
- [ ] **Implement Conversation scope** — Memories tied to a conversation ID
- [ ] **Implement Session scope** — Temporary working context (expires after session)
- [ ] **Implement Agent scope** — Memories tied to a specific agent ID

---

## Phase 3 — Memory Intelligence

**Goal:** Implement the core memory intelligence pipeline — capture, classify, retrieve, and construct context.

### Memory Capture

- [ ] **Conversation analysis** — Detect valuable information from chat messages
- [ ] **Extraction rules** — Configurable patterns for extracting different memory types
- [ ] **Automatic memory creation** — Create memories from detected information
- [ ] **Memory deduplication** — Detect when new information duplicates existing memories
- [ ] **Capture pipeline endpoint** — `POST /api/Memory/capture` for submitting interactions

### Memory Classification

- [ ] **Type classification** — Automatically categorize memories by type
- [ ] **Scope detection** — Determine appropriate scope from context
- [ ] **Confidence scoring** — Rate how confident the system is in each memory
- [ ] **Classification rules** — Configurable rules for classification

### Memory Lifecycle

- [ ] **Superseding** — When new information contradicts old, mark old as superseded
- [ ] **Expiration** — Automatic expiration of temporary memories
- [ ] **Archival** — Move old memories to archive state
- [ ] **Change tracking** — Record all memory changes with reasons

### Semantic Retrieval

- [ ] **Embedding generation** — Generate embeddings for memories and queries
- [ ] **Vector storage** — Store embeddings alongside memories
- [ ] **Similarity search** — Find semantically relevant memories
- [ ] **Hybrid retrieval** — Combine keyword and semantic search
- [ ] **Relevance ranking** — Score and rank retrieved memories

---

## Phase 4 — Context Intelligence

**Goal:** Build intelligent context construction that selects the right memories for each request.

### Context Construction

- [ ] **Token-aware selection** — Fit memories within token budget
- [ ] **Priority ranking** — Global > User > Project > Conversation > Session > Agent
- [ ] **Recency weighting** — Prefer recent memories over old ones
- [ ] **Type prioritization** — Instructions before preferences, constraints before goals
- [ ] **Context templates** — Configurable templates for how context is presented to LLMs

### Advanced Retrieval

- [ ] **Temporal relevance** — Consider when memories were created/updated
- [ ] **Relationship awareness** — Understand connections between memories
- [ ] **Conflict detection** — Identify contradictory memories and resolve them
- [ ] **Context窗口 management** — Handle token limits gracefully

---

## Phase 5 — Multi-Developer and Team Features

**Goal:** Support multiple developers and teams sharing memory.

### Multi-Developer Support

- [ ] **User authentication** — API key or OAuth-based auth
- [ ] **User-scoped memories** — Each user has their own memory space
- [ ] **Profile per user** — Multiple developer profiles
- [ ] **User preferences** — Per-user configuration

### Team Features

- [ ] **Shared knowledge bases** — Team-wide knowledge documents
- [ ] **Role-based access** — Control who can read/write which memories
- [ ] **Team profiles** — Shared team context and conventions
- [ ] **Knowledge inheritance** — Global → Team → User scope hierarchy

### Web UI

- [ ] **Memory dashboard** — View and manage memories
- [ ] **Knowledge editor** — Edit knowledge documents
- [ ] **Profile editor** — Edit developer profiles
- [ ] **Search interface** — Search and filter memories
- [ ] **Analytics** — Memory usage and effectiveness metrics

---

## Phase 6 — Ecosystem

**Goal:** Make DeveloperMemory.Api a reusable platform for AI memory.

### Plugin System

- [ ] **Custom capture hooks** — Allow custom extraction logic
- [ ] **Custom retrieval strategies** — Pluggable retrieval algorithms
- [ ] **Custom context templates** — Customizable context formatting
- [ ] **Provider plugins** — Support for non-OpenAI providers

### IDE Integrations

- [ ] **VS Code extension** — Native VS Code integration
- [ ] **JetBrains plugin** — IntelliJ/PyCharm integration
- [ ] **Neovim plugin** — Terminal-based integration

### Learning and Adaptation

- [ ] **Feedback loop** — Learn from which memories are actually used
- [ ] **Automatic pruning** — Remove memories that are never retrieved
- [ ] **Preference learning** — Infer preferences from usage patterns

---

## Principles

1. **Honesty over ambition.** Nothing is "done" until it ships in code.
2. **Simple now, extensible later.** Architecture is minimal with clear extension points.
3. **Each phase is independently valuable.** A developer can use the tool at the end of any phase.
4. **Memory first, AI second.** The core product is memory management, not LLM proxying.
5. **Local-first, cloud-ready.** Start with local SQLite, design for future PostgreSQL/cloud.
