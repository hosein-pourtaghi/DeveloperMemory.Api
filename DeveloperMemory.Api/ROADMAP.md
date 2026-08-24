# Roadmap

This roadmap separates work into clear phases. Each phase builds on the previous one. Nothing is described as "done" unless it is actually implemented in code.

---

## Current Phase — Foundation

**Goal:** Establish the core architecture, data formats, and a working prototype.

**Status:** Design and documentation complete. Implementation not started.

### Deliverables

- [ ] .NET 10.0 project setup (`DeveloperMemory.Api.csproj`, `Program.cs`, `appsettings.json`)
- [ ] OpenAI-compatible request/response models
- [ ] `POST /v1/chat/completions` endpoint (non-streaming first)
- [ ] `GET /v1/models` and `GET /v1/models/{modelId}` endpoints
- [ ] `KnowledgeService` — Load Markdown documents, parse YAML frontmatter, keyword search
- [ ] `ProfileService` — Load developer profiles from Markdown files
- [ ] `PromptBuilder` — Enrich system message with profile and knowledge context
- [ ] `FreeLlmApiClient` — Forward requests to downstream OpenAI-compatible provider
- [ ] Configuration system via `appsettings.json`
- [ ] Basic error handling (OpenAI-compatible error responses)
- [ ] Health check endpoint (`GET /health`)

### Out of Scope for This Phase

- Streaming
- Auto model selection
- Token tracking
- Management API endpoints
- Authentication

---

## V1 — Production-Ready Local Tool

**Goal:** Complete a feature-complete local tool that a developer can use daily with their AI coding assistant.

### Features

- [ ] SSE streaming support for `/v1/chat/completions`
- [ ] Auto model selection (plan vs build mode detection)
- [ ] Token tracking and logging (three-stage: incoming → enriched → response)
- [ ] `GET /api/Knowledge` — Search knowledge base
- [ ] `GET /api/Knowledge/documents` — List all documents
- [ ] `GET /api/Knowledge/{id}` — Get document by ID
- [ ] `POST /api/Knowledge` — Create a new document
- [ ] `POST /api/Knowledge/reindex` — Reload and reindex documents
- [ ] `GET /api/Profiles` — List developer profiles
- [ ] `POST /api/Profiles` — Load a profile from file
- [ ] Request logging middleware for debugging
- [ ] Multimodal content handling (array content fields)
- [ ] Global exception middleware
- [ ] Swagger UI for API exploration
- [ ] Comprehensive error handling for all edge cases

### Acceptance Criteria

- [ ] `dotnet build` succeeds with zero errors
- [ ] All endpoints return OpenAI-compatible responses
- [ ] Streaming works end-to-end with Cline or similar client
- [ ] Token metrics appear in console and log files
- [ ] Auto model selection correctly routes plan vs build tasks
- [ ] Knowledge search returns relevant results ordered by score

---

## V2 — Semantic Search and Persistent Storage

**Goal:** Move beyond keyword matching to intelligent retrieval and durable storage.

### Features

- [ ] Embedding-based semantic search (replaces or augments keyword search)
- [ ] Persistent storage (database or file-based index that survives restarts)
- [ ] Authentication and API key management
- [ ] Improved relevance scoring with embeddings
- [ ] Document versioning and change tracking
- [ ] Bulk import/export of knowledge documents

---

## V3 — Multi-Developer and Team Features

**Goal:** Support teams and shared knowledge bases.

### Features

- [ ] Multi-developer profiles with team scoping
- [ ] Role-based knowledge access (what each team member sees)
- [ ] Shared knowledge bases across team members
- [ ] Web UI for managing profiles, knowledge, and monitoring usage
- [ ] Cost analytics dashboard (token usage per model, per mode, over time)
- [ ] Usage reporting and team insights

---

## V4 — Ecosystem and Integration

**Goal:** Expand beyond a standalone tool into an ecosystem.

### Features

- [ ] Plugin system for custom enrichment hooks
- [ ] Native IDE extensions (VS Code, JetBrains)
- [ ] Learning from interactions to improve context selection
- [ ] Support for non-OpenAI protocols (if needed)
- [ ] Community knowledge sharing (optional, opt-in)

---

## Principles

1. **Honesty over ambition.** Nothing is "done" until it ships in code.
2. **Simple now, extensible later.** Architecture should be minimal at each phase with clear extension points.
3. **Working software over documentation.** Documentation guides implementation; it does not replace it.
4. **Each phase is independently valuable.** A developer should be able to use the tool at the end of any phase.
