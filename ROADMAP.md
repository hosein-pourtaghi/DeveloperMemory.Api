# Development Roadmap

**Last updated:** 2026-09-09 (V2 phase record added; PostgreSQL runtime verification executed)

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

## Post-V1 Phases (implemented in source; verified 2026-09-09)

These phases were implemented after Phase G and live in the squashed history
commit `f964aca`. Their reports are in `Phase-S-Report.md`, `Phase-T-Report.md`,
`Phase-U-Report.md`, and `PHASE_W_REPORT.md`:

### Phase R — Workspace/Project Context ✅
- `MemoryScope.Workspace` + `WorkspaceId` on `MemoryEntry`; scope-resolved retrieval
- Verified end-to-end by `tests/PhaseU_E2E_Verification.sh`

### Phase S — Semantic/Relevance Ranking Pipeline ✅
- `MemoryRetrievalService` pipeline: `KeywordRetrievalProvider` →
  `PrivacyFilter` → `LifecycleFilter` → `RelevanceRanker` → `CharacterContextBudgeter`
- Owner-isolated retrieval providers (`IRetrievalRanker`, `IContextBudgeter`)

### Phase T — Agent Context Intelligence ✅ (this is the "AgentContext" work)
- `IAgentContextProvider`/`AgentContextProvider`: resolves `AgentContext`
  (agent id/type, `TaskIntent`, confidence, project/workspace hints) from request fields
- `IAgentContextService`/`AgentContextService`: agent-aware retrieval that enriches
  the Phase-S `RetrievalRequest` (never bypasses the existing pipeline)
- `AgentContextController` (`/api/AgentContext`) and `AgentMemoryController`
- Registered in `ServiceCollectionExtensions` ("Phase T" section)

### Phase U — Real PostgreSQL + Kestrel E2E Verification ✅
- `tests/PhaseU_E2E_Verification.sh` — real HTTP + real PostgreSQL verification script

### Phase W — AgentContext integrated into the OpenAI-compatible gateway ✅
- `OpenAIChatCompletionController` resolves an `AgentContext` (when the request
  carries `agent_id`/agent fields) and passes it to `IPromptIntelligenceEngine.ProcessAsync`
- `OpenAIRequestResponse` carries the agent fields; `AgentContextController` scope
  validation returns BadRequest for empty agent ids
- Isolation verified: workspace-A memories are not returned for workspace-B requests;
  private memories are not returned cross-user
- See `PHASE_W_REPORT.md` for the integration flow

### Phase X — API contract coverage ✅
- `PhaseXApiContractTests` (24 facts) covering gateway contract stability

**Note on "V2-2 / V2-3 / V2-4" labels:** No phase with these identifiers exists in
this repository. In particular there is no "Assistant/Orchestrator Core" and no
"Dynamic Agent System" in source; the implemented architecture is exactly the
Phase T/W AgentContext subsystem described above.

---

## Test Baseline History

```
Phase A:     ~90 tests
Phase B:      140 tests
Phase C:      520 tests
Phase D:      523 tests
Phase D.1:    530 tests
Phase E:      554 tests
Phase F:      598 tests
Phase G:      598 tests (runtime verification, no test changes)
V2 (R/S/T/U/W/X): 1,014 tests
pgvector semantic retrieval: 1,052 tests ← CURRENT (verified 2026-09-11)
```

---

## V1 Readiness Assessment

| Area | Status | Notes |
|------|--------|-------|
| Memory | ✅ Complete | Persistent CRUD, lifecycle, scopes, ownership |
| Retrieval | ✅ Complete | Keyword, semantic, hybrid — all owner-isolated |
| Prompt Intelligence | ⚠️ Partially complete | Pipeline verified; external LLM not configured |
| Gateway | ⚠️ Partially complete | Auth + enrichment verified; external forwarding verified on Railway deployment (needs provider keys for real model calls) |
| Security | ✅ Complete | API keys, ownership, rate limiting, audit trail |
| Persistence | ✅ Complete | PostgreSQL with InMemory fallback |
| Reliability | ✅ Complete | Startup, health, error handling, logging |
| Observability | ✅ Complete | Security audit, request logging |

---

## Remaining Considerations

- ~~**PostgreSQL Runtime Verification**~~ — Executed 2026-09-09 with a local
  PostgreSQL 14 server (no Docker): all Postgres-backed test suites pass and both
  migrations apply cleanly. See `CURRENT_STATUS.md` for the verified baseline.
- **JWT Authentication** — For browser-based applications (out of scope)
- ~~**Controller Integration Tests**~~ — Implemented (`PostgresE2EFactory` +
  `PhaseWIntegrationTests` + `PhaseXApiContractTests`)
- **FreeLLMApi Integration** — Requires valid API key
