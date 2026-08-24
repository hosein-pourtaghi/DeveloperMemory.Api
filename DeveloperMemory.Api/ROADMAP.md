# ROADMAP.md — Development Roadmap

*Updated based on actual implementation audit. Last verified: 2026-08-24.*

## Current Phase: V1 Completion & Hardening

The core V1 implementation exists and is functionally complete. The current work focuses on completion, quality, and reliability.

---

### Phase 1: Recovery & Audit ✅ Complete

- [x] Audit actual repository state (source code exists, contrary to previous claims)
- [x] Inventory all existing components
- [x] Identify working, partial, and missing components
- [x] Align documentation with reality

### Phase 2: Code Quality Fixes ✅ Complete (this audit)

- [x] Remove misleading unused `BuildModeIndicators` array in `ModeDetector`
- [x] Fix frontmatter parser to handle `name` as alias for `title` in knowledge documents
- [x] Fix YAML escaping in `CreateDocumentAsync` for titles with special characters

### Phase 3: Build Verification (Next)

- [ ] Verify `dotnet build` succeeds with 0 errors
- [ ] Verify `dotnet restore` resolves all packages
- [ ] Run the application and verify health check returns healthy
- [ ] Test `/v1/chat/completions` non-streaming with a real provider
- [ ] Test `/v1/chat/completions` streaming with a real provider
- [ ] Verify knowledge loading and search work end-to-end
- [ ] Verify profile loading works end-to-end
- [ ] Verify token metrics appear in console and log files
- [ ] Verify Swagger UI loads and shows correct API spec

### Phase 4: Test Infrastructure

- [ ] Create test project (`DeveloperMemory.Api.Tests`)
- [ ] Add unit tests for `KnowledgeService` (frontmatter parsing, search scoring)
- [ ] Add unit tests for `ProfileService` (frontmatter parsing)
- [ ] Add unit tests for `PromptBuilder` (context assembly, message preservation)
- [ ] Add unit tests for `ModeDetector` (plan/build/unknown detection)
- [ ] Add unit tests for `TokenEstimator` (token counting accuracy)
- [ ] Add integration tests for `KnowledgeController` (CRUD endpoints)
- [ ] Add integration tests for `OpenAIChatCompletionController` (with mock provider)

### Phase 5: Production Readiness

- [ ] Add Dockerfile for containerized deployment
- [ ] Add CI/CD pipeline (GitHub Actions: build, test, publish)
- [ ] Lock down CORS for production environments
- [ ] Add configuration validation at startup
- [ ] Add structured logging improvements (correlation IDs, request tracing)
- [ ] Add graceful shutdown handling for in-flight requests

---

## V1 Final Target

A reliable, well-tested, OpenAI-compatible memory gateway that:

- Loads developer profiles and knowledge from Markdown files
- Automatically enriches AI requests with relevant context
- Supports streaming and non-streaming completions
- Forwards to any OpenAI-compatible provider
- Has a solid test suite and clean deployment story

---

## V2+ — Future Capabilities

These are explicitly **out of scope** for V1. They represent the natural evolution path.

### V2: Enhanced Retrieval

- [ ] Persistent database (SQLite or PostgreSQL) for knowledge and profiles
- [ ] Embeddings generation for semantic search
- [ ] Vector store for similarity-based retrieval
- [ ] Hybrid search (keyword + semantic)
- [ ] Relevance feedback and result ranking improvements

### V3: Intelligence Layer

- [ ] Automatic memory extraction from conversations
- [ ] Decision and historical memory storage
- [ ] Context budget management (token limits for enriched prompts)
- [ ] Cross-session learning and preference refinement
- [ ] Knowledge freshness tracking and staleness detection

### V4: Multi-User & Team

- [ ] User authentication and authorization
- [ ] Multi-tenant knowledge isolation
- [ ] Team-shared knowledge bases
- [ ] Role-based access control
- [ ] Audit logging for knowledge changes

### V5: Integration Ecosystem

- [ ] IDE plugins (VS Code, JetBrains)
- [ ] Webhook support for external knowledge ingestion
- [ ] Knowledge sync with documentation systems (Confluence, Notion, GitHub Wiki)
- [ ] Analytics dashboard for context usage and effectiveness
- [ ] Plugin architecture for custom retrieval strategies
