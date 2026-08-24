# Roadmap

*Last updated: 2026-08-24.*

## Product Evolution

```
Foundation (V1 Core)          ← We are here
    ↓
Production-Ready V1
    ↓
Semantic Retrieval (V2)
    ↓
Intelligent Context (V3)
    ↓
Team & Enterprise (V4)
```

---

## Foundation — V1 Core ✅ Complete

**Goal:** Prove that developer-authored Markdown files can automatically enrich AI coding assistant interactions.

**What was delivered:**

- [x] Developer identity loading from Markdown with YAML frontmatter
- [x] Project knowledge loading, indexing, and keyword search
- [x] Context assembly that preserves conversation history
- [x] OpenAI-compatible chat completions endpoint with streaming
- [x] Provider forwarding to any OpenAI-compatible LLM
- [x] Mode detection (plan vs build) with automatic model selection
- [x] Token tracking and request logging
- [x] Management APIs for knowledge and profiles
- [x] Unit test suite (50+ tests)
- [x] Stable document/profile IDs (deterministic from file paths)
- [x] Dead code cleanup and frontmatter parser fixes

---

## Production-Ready V1 — Current Phase

**Goal:** Make V1 reliable, verified, and deployable.

### Build & Runtime Verification

- [ ] Verify `dotnet build` succeeds with 0 errors (requires .NET 10 SDK)
- [ ] Run application and verify health check, Swagger, and basic API behavior
- [ ] Test chat completions (non-streaming) with a real provider
- [ ] Test chat completions (streaming) with a real provider
- [ ] Verify knowledge loading and search work end-to-end

### Testing

- [ ] Add integration tests for Knowledge API (CRUD endpoints)
- [ ] Add integration tests for Chat Completions API (with fake provider)
- [ ] Run full test suite and fix any failures

### Production Hardening

- [ ] Lock down CORS for production environments
- [ ] Add configuration validation at startup
- [ ] Add Dockerfile for containerized deployment
- [ ] Add CI/CD pipeline (GitHub Actions: build, test, publish)
- [ ] Add graceful shutdown handling for in-flight requests

---

## V2 — Semantic Retrieval

**Goal:** Replace keyword search with embedding-based retrieval for more relevant context.

**Why this matters:** Keyword matching misses conceptually related knowledge. A developer asking about "error handling" should also retrieve documents about "exception patterns" and "resilience" even if those exact words don't appear.

- [ ] Persistent database (SQLite or PostgreSQL) for knowledge and profiles
- [ ] Embeddings generation for knowledge documents
- [ ] Vector store for similarity-based retrieval
- [ ] Hybrid search (keyword + semantic)
- [ ] Relevance feedback and result ranking improvements

---

## V3 — Intelligent Context

**Goal:** Make context assembly smarter — not just more relevant, but more aware of what the AI actually needs.

- [ ] Context budget management (token limits for enriched prompts)
- [ ] Automatic memory extraction from conversations
- [ ] Decision and historical memory storage
- [ ] Cross-session learning and preference refinement
- [ ] Knowledge freshness tracking and staleness detection

---

## V4 — Team & Enterprise

**Goal:** Support teams and organizations with shared knowledge and access control.

- [ ] User authentication and authorization
- [ ] Multi-tenant knowledge isolation
- [ ] Team-shared knowledge bases
- [ ] Role-based access control
- [ ] Audit logging for knowledge changes
- [ ] IDE plugins (VS Code, JetBrains)
- [ ] Webhook support for external knowledge ingestion
- [ ] Knowledge sync with documentation systems (Confluence, Notion, GitHub Wiki)
