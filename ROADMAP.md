# Development Roadmap

**Last updated:** 2026-08-28 (Phase J.1 complete)

---

## Completed Phases

### Phase A — Architecture Consolidation ✅
### Phase B — Security Baseline ✅
### Phase C — Test Recovery ✅ (520 → 523 tests)
### Phase D — Authentication & Ownership ✅ (523 → 530 tests)
### Phase D.1 — Retrieval Ownership Completion ✅
- OwnerId enforced in all retrieval providers
- PrivacyFilter defense-in-depth
- 7 cross-user retrieval isolation tests
- Final: **530 tests, 0 failures**

### Phase E — Security Hardening ✅
- API-key authentication lifecycle (expiration, revocation, rotation)
- Fail-closed OwnerId enforcement
- Rate limiting (fixed window)
- Security audit trail (in-memory)
- 23 security tests
- Final: **554 tests, 0 failures**

### Phase F — Persistent Security State & Production Hardening ✅
- Persistent API-key storage in PostgreSQL (salted SHA-256 hashes)
- Persistent security audit trail in PostgreSQL (append-only)
- Per-identity rate-limit partitioning (no global bucket)
- Endpoint-category-specific rate limits (general/expensive/keymanagement)
- Config-based keys demoted to development bootstrap
- API key hasher (salted SHA-256, constant-time comparison)
- EF Core migration for ApiKeys + SecurityAuditLog tables
- 44 new tests (persistent keys, audit, rate limiting, hashing)
- Final: **598 tests, 0 failures**

### Phase G — End-to-End Runtime Verification ✅
- Application startup verified (InMemory backend)
- 24 runtime smoke tests — all passing
- Health check: 200 OK, no auth required
- Authentication: valid key → 200, invalid/missing → 401
- API key lifecycle: create → authenticate → revoke → 401 → rotate → overlap
- Memory CRUD: create, stats, retrieval — all working
- Cross-user isolation: User A cannot see User B's memories
- Direct ID authorization: cross-owner access returns 404
- Rate limiting: per-identity, endpoint-category — no false rejections
- Security audit: events recorded, no raw secrets leaked
- Gateway: auth enforced, enrichment pipeline reachable
- Error handling: invalid resources → appropriate 404/400
- No runtime bugs discovered during verification

---

### Phase I — Retrieval Intelligence & Verification ✅
- Keyword retrieval now excludes all non-active lifecycle states and expired memories at the database query boundary
- Owner isolation remains fail-closed across retrieval and direct memory access
- Project, workspace, private-scope, category, and explicit-scope filtering verified
- Missing workspace/private boundary identifiers fail closed
- Deterministic relevance ranking and result limits retained
- Native PostgreSQL retrieval coverage added for lifecycle, project, workspace, private, and owner isolation
- Semantic/vector and hybrid providers are selectable through the active application retrieval pipeline; Auto chooses hybrid only when semantic infrastructure is available
- Phase J.1 restored seven service regression behaviors, corrected hybrid semantic-score merging, normalized ranking, and added provider-selection coverage
- Docker/container infrastructure was not used or introduced

## Test Baseline History

```
Phase A:     ~90 tests
Phase B:      140 tests
Phase C:      520 tests
Phase D:      523 tests
Phase D.1:    530 tests
Phase E:      554 tests
Phase F:      598 tests ← CURRENT
Phase G:      598 tests (runtime verification, no test changes)
Phase J.1:    631 tests (retrieval regression restoration and activation coverage)
Phase K:      634 tests (ownership-aware prompt processing history)
```

---

## V1 Readiness Assessment

| Area | Status | Notes |
|------|--------|-------|
| Memory | ✅ Complete | Persistent CRUD, lifecycle, scopes, ownership |
| Retrieval | ✅ Implemented with optional semantic mode | Keyword is the default; Semantic, Hybrid, and Auto selection are wired through the application service with graceful keyword fallback |
| Prompt Intelligence | ⚠️ Partially complete | Pipeline verified; external LLM not configured |
| Gateway | ⚠️ Partially complete | Auth + enrichment verified; external forwarding not verified |
| Security | ✅ Complete | API keys, ownership, rate limiting, audit trail |
| Persistence | ✅ Complete | PostgreSQL with InMemory fallback |
| Reliability | ✅ Complete | Startup, health, error handling, logging |
| Observability | ✅ Complete | Security audit, request logging |

---

## Remaining Considerations

- **External semantic runtime verification** — The local default has semantic retrieval disabled because no embedding credentials are configured; provider-enabled Kestrel verification remains environment-dependent
- **Authenticated HTTP retrieval matrix** — Application/infrastructure retrieval is covered; dedicated Kestrel endpoint coverage remains optional follow-up work
- **Prompt processing history API** — History reads are now owner-scoped through the application service; broader query/filter authorization coverage remains future work
- **JWT Authentication** — For browser-based applications (out of scope)
- **Controller Integration Tests** — Not yet implemented
- **FreeLLMApi Integration** — Requires valid API key
