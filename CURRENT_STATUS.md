# Current Status

**Last verified:** September 11, 2026 (full build + test execution against real PostgreSQL with pgvector)
**Version:** .NET 10.0
**Branch:** master

---

## Build & Test Baseline (executed, not inferred)

```
Restore:      ✅ All projects restored
Build:        ✅ 0 errors (Debug configuration)
Warnings:     683 (NuGet advisories, xUnit1031 blocking-task analyzer
                    warnings in tests, pre-existing CS8602/CS0168)
Discovered:   1,052
Passed:       1,052
Failed:          0
Skipped:         0
```

### Per-Project Counts (actual)

```
DeveloperMemory.Domain.Tests:            38
DeveloperMemory.Application.Tests:      597
DeveloperMemory.Infrastructure.Tests:   150
DeveloperMemory.Api.Tests:              267
────────────────────────────────────────────
TOTAL:                                  1,052
```

### pgvector semantic retrieval verification (2026-09-11)

PostgreSQL semantic/hybrid retrieval is verified operational against real
PostgreSQL + pgvector (local PostgreSQL 14 with pgvector 0.8.0):

- The `20260909164235_EnablePgvectorSemanticRetrieval` migration applies cleanly
  to a fresh database: it creates the `vector` extension and converts
  `VectorEntries.Vector` from `real[]` to pgvector `vector` (data-preserving
  conversion through the bracketed text form; the Down path reverses it).
- `PostgresSemanticVectorStoreTests` (5 tests) verify: the column is a real
  pgvector `vector` type, vector upsert/search ranking via the `<=>` operator,
  upsert replace semantics, get/delete round-trip, and the full
  `SemanticRetrievalProvider` pipeline with owner isolation against real vectors.

Note: There is no consolidated `DeveloperMemory.Tests` project. The solution
contains exactly 4 test projects. Historical documents referencing a 5-project
test layout or a 419-method consolidated project are stale.

### PostgreSQL runtime verification (previously pending — now executed)

The Phase G "PostgreSQL runtime verification pending" gap is closed. A local
PostgreSQL 14 server was used (no Docker), matching the tests' expected
conventions (`developer`/`devpassword`):

- Both migrations (`20260828103251_InitialCreate`,
  `20260830083234_AddDiagnosticLogs`) apply cleanly to a fresh database.
- Migrated schema contains all 14 expected tables including `DiagnosticLogs`
  and `SecurityAuditLog` (the Phase W "missing DiagnosticLogs table" note was
  stale — the table is created by the `AddDiagnosticLogs` migration).
- `PostgresE2EFactory` boots the real `Program.cs` via `WebApplicationFactory`
  against a unique real PostgreSQL database per test class and exercises the
  full HTTP stack (auth → controller → engine → repository → PostgreSQL).
  All 267 Api tests pass, including the Postgres-backed E2E classes
  (`Postgres_ConversationalMemoryTests`, `Postgres_DiagnosticLoggingTests`,
  `Postgres_AgentMemoryApiTests`).
- `PostgresDbFixture` (Infrastructure) verifies persistence across DbContext
  recreation: memory CRUD/lifecycle, API-key lifecycle, append-only audit,
  and retrieval owner isolation — all 112 tests pass.
- Test databases: `developermemory_test` (Infrastructure fixture,
  reset per test class) and uniquely named `e2e_*` databases (Api factory,
  created/dropped per fixture).

---

## Runtime Verification Results (Phase G)

### Runtime Smoke-Test Matrix (InMemory Backend)

| Scenario                  | Result | Backend | Notes |
| ------------------------- | ------ | ------- | ----- |
| Startup                   | ✅ PASS | InMemory | Application starts successfully |
| Health                    | ✅ PASS | InMemory | 200 OK, no auth required |
| Development auth-free mode | ✅ PASS | InMemory | No credentials; protected endpoints receive local identity |
| Invalid API key           | ✅ PASS | InMemory | 401 Unauthorized |
| Valid config key auth     | ✅ PASS | InMemory | 200 OK |
| DB key creation           | ✅ PASS | InMemory | Raw key returned once only |
| DB key authentication     | ✅ PASS | InMemory | Newly created key authenticates |
| Key list (no secrets)     | ✅ PASS | InMemory | Metadata only, no KeyHash or raw secret |
| Key revocation            | ✅ PASS | InMemory | Revoked key → 401 |
| Key rotation              | ✅ PASS | InMemory | New key issued with overlap expiration |
| Memory create (User A)    | ✅ PASS | InMemory | Title + content required |
| Memory create (User B)    | ✅ PASS | InMemory | |
| Stats User A (≥1)         | ✅ PASS | InMemory | Owner-scoped |
| Stats User B (≥1)         | ✅ PASS | InMemory | Owner-scoped |
| Cross-user isolation (A)  | ✅ PASS | InMemory | A cannot see B's memory |
| Cross-user isolation (B)  | ✅ PASS | InMemory | B cannot see A's memory |
| Rate limiting             | ✅ PASS | InMemory | Normal requests pass within limits |
| Audit events              | ✅ PASS | InMemory | Events recorded, no raw secrets |
| Production auth boundary | ✅ PASS | Configuration | Non-Development selects API-key authentication |
| Gateway: reaches pipeline | ✅ PASS | InMemory | Request reaches enrichment (no LLM provider) |
| Invalid GUID → 404/400    | ✅ PASS | InMemory | 404 Not Found |

### Runtime Verification Summary

```
Total scenarios:    24
Passed:             24
Failed:              0
```

---

## Architecture Summary

- Clean Architecture: Domain ← Application ← Infrastructure ← API
- Persistent memory with PostgreSQL/InMemory fallback
- Memory lifecycle (Active, Superseded, Expired, Archived, Deleted)
- Memory Intelligence: extraction, conflict detection, ingestion
- Prompt Intelligence: analysis, context assembly, optimization, evaluation
- Retrieval: keyword, semantic, hybrid with owner isolation
- Environment-bound authentication: auth-free Development identity; API-key authentication in Production/non-Development
- Ownership enforcement at repository, retrieval, and filter levels
- Fail-closed OwnerId (missing OwnerId = no results)
- Rate limiting: per-identity partitioned, endpoint-category-specific
- Security audit trail: persistent PostgreSQL storage (append-only)
- CORS hardened
- Sensitive request logging protection

---

## Authentication & Security

- **Development:** Authentication credentials are not required. The existing `DevelopmentAuthenticationHandler` supplies a deterministic local identity so `[Authorize]` endpoints work immediately without login, JWTs, API keys, or auth headers.
- **Production/non-Development:** Authentication and authorization remain enabled and use the existing API-key Bearer-token flow.
- **Docker:** Docker is not an authentication mode. Behavior follows `ASPNETCORE_ENVIRONMENT`; the Dockerfile defaults to Production for deployed containers, while Compose explicitly uses Development for local convenience.
- **Model:** API Key via Bearer token outside Development
- **Key storage:** PostgreSQL (primary) + configuration (development bootstrap)
- **Secret handling:** Salted SHA-256 hashes — raw keys never persisted
- **Identity abstraction:** ICurrentUser (Application layer)
- **Ownership enforcement:** Server-derived OwnerId on all memory operations
- **Fail-closed:** Empty/missing OwnerId returns no results
- **Lifecycle:** Expiration, revocation, rotation with configurable overlap period
- **Rate limiting:** Per-identity partitioned (200 general, 50 expensive, 20 key management per minute)
- **Audit trail:** Persistent PostgreSQL append-only log (no raw secrets)
- **Key management:** CRUD endpoints (list, create, rotate, revoke, audit)

---

## Development Authentication

Normal local development is auth-free. Running `dotnet run --project src/DeveloperMemory.Api` uses the `Development` environment from `launchSettings.json`; protected endpoints receive the deterministic local identity automatically. No API key or authentication configuration is required.

The existing configuration-based API keys remain available for explicitly testing the real API-key handler. Production keys are created via `POST /api/ApiKey/create` and stored in PostgreSQL.

```json
{
  "dev-key-user-a-test-2024": "user-a",
  "dev-key-user-b-test-2024": "user-b"
}
```

**Note:** Development keys bypass database lookup. Production should remove config keys.

---

## Remaining Gaps

### Verification Gaps
1. **Rate-limit exhaustion** — Not tested at scale (too slow for smoke test)
2. **FreeLLMApi live forwarding** — Gateway chain verified in a prior Railway
   deployment; local verification requires a configured upstream API key

### Intentionally Deferred
3. **JWT for browser applications** — Out of scope for current architecture

### Closed Gaps (previously listed as pending)
- ~~PostgreSQL runtime ownership verification~~ — Executed with real PostgreSQL
  (see baseline above; all Postgres-backed suites pass).
- ~~Persistence after restart~~ — Verified by the Infrastructure
  `Postgres*_PersistenceTests` suites, which write through one `DbContext`, dispose
  it, and read back through a fresh one against real PostgreSQL.
- ~~Integration tests for controllers~~ — Implemented: `PostgresE2EFactory`
  boots the real app via `WebApplicationFactory` and exercises controllers over
  HTTP; `PhaseWIntegrationTests` and `PhaseXApiContractTests` cover gateway and
  contract behavior.

### No Blockers
The application is verified to work as intended against both InMemory and real
PostgreSQL backends.

---

## Known Non-Features (source-verified, for agent accuracy)

The following have been **claimed in some historical conversations** but do NOT
exist in source code. Do not document or rely on them:

- **Assistant/Orchestrator Core ("V2-2")** — No such component exists. The
  closest implemented equivalent is `IContextOrchestrator`/`ContextOrchestrator`
  (Phase 9) and `PromptIntelligenceEngine` (Phase 4).
- **Dynamic Agent System ("V2-3")** — No dynamic agent registry/runtime exists.
  What exists is the static **AgentContext subsystem** (Phase T):
  `IAgentContextProvider`/`AgentContextProvider` (resolves agent type, task
  intent, confidence from request hints) and `IAgentContextService`/
  `AgentContextService` (agent-aware retrieval over the Phase-S pipeline),
  exposed via `AgentContextController` and `AgentMemoryController`, and wired
  into the OpenAI-compatible gateway (`OpenAIChatCompletionController` resolves
  an `AgentContext` when the request carries agent fields).
- **"V2-4"** — No phase with this identifier is defined anywhere in the
  repository (docs, code, or tests). Any claims about its completion are
  unverifiable by construction.
