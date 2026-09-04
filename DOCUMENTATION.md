# Documentation Index

**Last updated:** 2026-08-27 (Phase C complete)

---

## Core Documentation

| Document | Description |
|----------|-------------|
| [AGENTS.md](AGENTS.md) | AI agent coding guide, architecture rules, project structure |
| [CLAUDE.md](CLAUDE.md) | Complete technical reference |
| [CURRENT_STATUS.md](CURRENT_STATUS.md) | Verified implementation state (520 tests, 0 failures) |
| [ROADMAP.md](ROADMAP.md) | Development phases and future direction |
| [PROJECT_VISION.md](PROJECT_VISION.md) | Current project vision, principles, and target architecture |
| [V2_VISION.md](V2_VISION.md) | Immutable V2 master specification and long-term architecture |

## Audit & Verification

| Document | Description |
|----------|-------------|
| [docs/ARCHITECTURE_VERIFICATION.md](docs/ARCHITECTURE_VERIFICATION.md) | Build/test/runtime verification results |
| [docs/ARCHITECTURE_AUDIT.md](docs/ARCHITECTURE_AUDIT.md) | Original architecture audit (superseded by verification doc) |
| [docs/TEST_MIGRATION_AUDIT.md](docs/TEST_MIGRATION_AUDIT.md) | Test migration from consolidated project |

## Configuration

| Document | Description |
|----------|-------------|
| [KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md) | Markdown knowledge document frontmatter format |

---

## Test Architecture

```
DeveloperMemory.Domain.Tests           38 tests  (entities, lifecycle, invariants)
DeveloperMemory.Application.Tests     327 tests (services, orchestration, intelligence)
DeveloperMemory.Api.Tests              81 tests  (controllers, contracts, abstractions)
DeveloperMemory.Infrastructure.Tests   74 tests  (repositories, persistence, retrieval)
─────────────────────────────────────────────────────────────────────────────────────────
TOTAL                                 520 tests
```

All tests are provider-independent (mocks/fakes/deterministic implementations).
No tests require external services (FreeLLMApi, PostgreSQL) to pass.

---

## Key Architecture Decisions

1. **Clean Architecture boundaries** — Domain ← Application ← Infrastructure ← API
2. **Provider independence** — `IModelGateway` abstraction; swap providers via DI
3. **Dual memory systems** — KnowledgeService (file-based) + MemoryService (PostgreSQL), orchestrated by `IMemoryRetriever`
4. **Prompt Intelligence Engine** — Central orchestration for analysis, retrieval, optimization, evaluation
5. **Environment-specific CORS** — Configurable via `Cors:AllowedOrigins`
6. **No Redis** — Removed; was unused infrastructure

---

## Remaining Critical Gap

> **Authentication, authorization, and multi-user memory ownership isolation.**

See [ROADMAP.md](ROADMAP.md) for the planned Phase D.
