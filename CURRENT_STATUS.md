# Current Status

**Last verified:** August 29, 2026
**Version:** .NET 10.0
**Branch:** main

---

## Build & Test Baseline

```
Restore:      ✅ All projects restored
Build:        ✅ 0 errors (Release configuration)
Warnings:     68 (NuGet advisories only)
Discovered:   598
Passed:       598
Failed:       0
Skipped:      0
```

### Per-Project Counts

```
DeveloperMemory.Domain.Tests:            38
DeveloperMemory.Application.Tests:      327
DeveloperMemory.Infrastructure.Tests:    92
DeveloperMemory.Api.Tests:              141
────────────────────────────────────────────
TOTAL:                                  598
```

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
1. **PostgreSQL runtime ownership verification** — InMemory tested; PostgreSQL not runtime verified (Docker daemon not available)
2. **Persistence after restart** — Cannot verify with InMemory backend (data lost on restart)
3. **Rate-limit exhaustion** — Not tested at scale (too slow for smoke test)

### Intentionally Deferred
4. **JWT for browser applications** — Out of scope for current architecture
5. **Integration tests for controllers** — Not yet implemented
6. **FreeLLMApi integration** — Requires valid API key (not configured)

### No Blockers
The application is verified to work as intended in InMemory mode. PostgreSQL runtime verification is pending Docker/infrastructure availability.
