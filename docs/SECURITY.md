# Security Architecture

**Last verified:** August 27, 2026 (Phase G — End-to-End Runtime Verification)

---

## Authentication Model

**Mechanism:** API Key via Bearer token

All protected endpoints require a valid API key sent as:
```
Authorization: Bearer <api-key>
```

### Key Storage

| Storage | Purpose | Production? |
|---------|---------|-------------|
| PostgreSQL `ApiKeys` table | Primary key store | ✅ |
| `appsettings.json` `Authentication:ApiKeys` | Development bootstrap | ❌ Dev only |

**Secret handling:** Raw API keys are hashed with salted SHA-256 before storage.
The raw key is only shown once at creation time. Never persisted in plaintext.

### Key Lifecycle States

| State | Auth Behavior |
|-------|--------------|
| Active (not expired, not revoked) | ✅ Authenticated |
| Expired (past ExpiresAt) | ❌ Rejected |
| Revoked (RevokedAt set) | ❌ Rejected |
| Unknown (not in DB or config) | ❌ Rejected |

### Authentication Flow

```
Bearer token received
    ↓
Try database lookup by key prefix, verify with salted hash
    ↓ (if not found)
Fall back to configuration-based keys
    ↓ (if not found)
Return 401 Unauthorized
    ↓ (found)
Check lifecycle: Revoked? Expired?
    ↓ (valid)
Record usage, create ClaimsPrincipal
    ↓
ICurrentUser resolves OwnerId from claims
```

### Key Rotation

1. New key is issued with full expiration
2. Old key's `ReplacedByKeyId` is set to new key
3. Old key's `ExpiresAt` is set to overlap period (configurable, default: 7 days)
4. Both keys work during overlap
5. After overlap, old key expires

### Configuration

Development keys (config-based):
```json
{
  "Authentication": {
    "DefaultExpirationDays": 90,
    "RotationOverlapDays": 7,
    "ApiKeys": [ ... ]
  }
}
```

Production keys are created via `POST /api/ApiKey/create` and stored in PostgreSQL.

---

## Runtime Security Verification (Phase G)

The following security properties were **runtime verified** via HTTP requests against the running application:

| Property | Verified | Method |
|----------|----------|--------|
| No credentials → 401 | ✅ | HTTP GET without auth header |
| Invalid key → 401 | ✅ | HTTP GET with bad key |
| Valid config key → 200 | ✅ | HTTP GET with dev key |
| DB key creation | ✅ | HTTP POST /api/ApiKey/create |
| DB key authentication | ✅ | HTTP GET with newly created key |
| DB key revocation → 401 | ✅ | HTTP POST /api/ApiKey/revoke, then auth attempt |
| Key rotation with overlap | ✅ | HTTP POST /api/ApiKey/rotate |
| Key list: no secrets | ✅ | HTTP GET /api/ApiKey — no raw key or KeyHash in response |
| Cross-user isolation | ✅ | User A cannot see User B's memory |
| Cross-owner direct ID → 404 | ✅ | HTTP GET with other user's memory ID |
| Rate limiting | ✅ | Normal requests pass within limits |
| Audit events: no raw keys | ✅ | HTTP GET /api/ApiKey/audit — no raw secrets in records |
| Gateway auth → 401 | ✅ | HTTP POST /v1/chat/completions without auth |

---

## Memory Ownership Model

### Identity Flow

```
API Key → ClaimsPrincipal → ICurrentUser → OwnerId
```

Ownership is derived server-side from the authenticated principal. Client-supplied `userId` or `ownerId` fields are ignored for ownership.

### Ownership Enforcement

Enforced at **three levels** (defense in depth):

1. **Repository level:** `WHERE OwnerId = @ownerId` on all queries
2. **Retrieval level:** Keyword, semantic, and hybrid providers filter by OwnerId
3. **PrivacyFilter level:** OwnerId check as Rule 0 before scope filtering

### Fail-Closed Behavior

If `OwnerId` is empty or missing:
- **Keyword retrieval:** Returns no results
- **Semantic retrieval:** Returns no results
- **PrivacyFilter:** Skips all memories
- **Repository queries:** No cross-owner data returned

Empty OwnerId never means "return all."

### Scope Semantics

| Scope | Visibility |
|-------|-----------|
| Global | Owner-scoped Global memories |
| Project | Owner-scoped, within specified ProjectId |
| Workspace | Owner-scoped, within specified WorkspaceId |
| Private | Owner-scoped, within specified UserId |

**Global does not mean shared across users.** Global means visible to the owner from any context.

---

## Rate Limiting

Per-identity partitioned rate limiting using `PartitionedRateLimiter`:

| Policy | Limit | Partitioning | Endpoints |
|--------|-------|-------------|-----------|
| general | 200 requests/min | userId or IP | Memory CRUD, Projects, Health |
| expensive | 50 requests/min | userId or IP | query, retrieve, analyze, embedding, gateway |
| keymanagement | 20 requests/min | userId or IP | /api/ApiKey/* |

**Partitioning key:** `user:{userId}` for authenticated requests, `ip:{remoteIp}` for unauthenticated.
One owner cannot consume another owner's rate-limit budget.

Rejected requests receive HTTP 429 Too Many Requests.

---

## Security Audit Trail

### Storage

| Mode | Implementation |
|------|---------------|
| PostgreSQL | `PersistentSecurityAuditService` → `SecurityAuditLog` table (production) |
| In-Memory | `InMemorySecurityAuditService` → `ConcurrentBag` (tests/development) |

### Events Captured

| Event Type | When |
|-----------|------|
| AuthenticationSuccess | Valid key used |
| AuthenticationFailure | Invalid key |
| InvalidApiKeyAttempt | Unknown key presented |
| ExpiredApiKeyAttempt | Expired key presented |
| RevokedApiKeyAttempt | Revoked key presented |
| KeyCreated | New key issued |
| KeyRotated | Key replaced |
| KeyRevoked | Key revoked |
| RateLimitRejected | 429 returned |
| AuthorizationFailure | Cross-user access attempted |
| OwnershipViolationAttempt | Detected boundary violation |

### Sensitive Data Exclusions

Audit records **never** contain:
- Raw API keys
- API-key hashes (unless explicitly required)
- Authorization headers
- Bearer tokens
- Passwords
- Full private memory content
- Full prompts
- Unnecessary request payloads

### Retention

Append-only. No automatic cleanup. Entries are persisted indefinitely.
Background cleanup may be added in a future phase.

---

## HTTP Security Behavior

| Scenario | Response |
|----------|----------|
| No credentials | 401 Unauthorized |
| Invalid key | 401 Unauthorized |
| Expired key | 401 Unauthorized |
| Revoked key | 401 Unauthorized |
| Cross-user access | 404 Not Found (not 403) |
| Rate limited | 429 Too Many Requests |

Cross-user access returns 404 to prevent information leakage about resource existence.

---

## Known Limitations

1. **No JWT** — Browser apps need a different auth scheme
2. **InMemory rate limits** — Rate limit counters reset on restart
3. **No key background cleanup** — Expired keys remain until manually cleaned
4. **PostgreSQL ownership** — Runtime verified only via InMemory mode
5. **No integration tests** — Controller endpoints not integration-tested
