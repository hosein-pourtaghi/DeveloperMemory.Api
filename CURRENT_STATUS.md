# Current Status

**Last verified:** August 28, 2026 (Phase J.1 — Retrieval activation correction and verification)
**Version:** .NET 10.0
**Branch:** main

---

## Build & Test Baseline

```
Restore:      ✅ All projects restored
Build:        ✅ 0 errors (Release configuration)
Warnings:     68 (NuGet advisories only)
Discovered:   634
Passed:       634
Failed:       0
Skipped:      0
```

### Per-Project Counts

```
DeveloperMemory.Domain.Tests:            38
DeveloperMemory.Application.Tests:      333
DeveloperMemory.Infrastructure.Tests:   122
DeveloperMemory.Api.Tests:              141
────────────────────────────────────────────
TOTAL:                                  634
```

---

## Runtime Verification Results (Phase G)

### Runtime Smoke-Test Matrix (InMemory Backend)

| Scenario                  | Result | Backend | Notes |
| ------------------------- | ------ | ------- | ----- |
| Startup                   | ✅ PASS | InMemory | Application starts successfully |
| Health                    | ✅ PASS | InMemory | 200 OK, no auth required |
| No authentication         | ✅ PASS | InMemory | 401 Unauthorized |
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
| Gateway: no auth → 401   | ✅ PASS | InMemory | 401 Unauthorized |
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
- Retrieval: keyword, semantic, and hybrid providers are selectable through the application retrieval service; Auto falls back to keyword when semantic retrieval is unavailable
- Retrieval safeguards: owner, project, workspace, private-scope, lifecycle, category filtering, deterministic ranking, and bounded results
- API Key authentication with persistent lifecycle management (PostgreSQL)
- Ownership enforcement at repository, retrieval, and filter levels
- Fail-closed OwnerId (missing OwnerId = no results)
- Rate limiting: per-identity partitioned, endpoint-category-specific
- Security audit trail: persistent PostgreSQL storage (append-only)
- CORS hardened
- Sensitive request logging protection

---

## Authentication & Security

- **Model:** API Key via Bearer token
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

## Development API Keys

Configuration-based keys in `appsettings.json` are **development bootstrap credentials only**.
Production keys are created via `POST /api/ApiKey/create` and stored in PostgreSQL.

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
1. **External semantic runtime verification** — Semantic/hybrid selection and score propagation are implemented and unit-tested, but the default local configuration has no external embedding provider enabled, so semantic HTTP runtime behavior was not exercised.
2. **HTTP retrieval integration coverage** — Retrieval behavior is covered at application/infrastructure layers; a dedicated authenticated Kestrel retrieval matrix remains optional follow-up work.
3. **Rate-limit exhaustion** — Not tested at scale (too slow for smoke test).

### Intentionally Deferred
4. **JWT for browser applications** — Out of scope for current architecture
5. **Integration tests for controllers** — Not yet implemented
6. **FreeLLMApi integration** — Requires valid API key (not configured)

### No Blockers
Keyword retrieval is verified against native PostgreSQL with fail-closed ownership, scope/project isolation, lifecycle filtering, deterministic ranking, and bounded results. Semantic and hybrid retrieval are now selectable through the application service when configured; the default local configuration remains keyword-only because no external embedding credentials are enabled. Docker was not used.
