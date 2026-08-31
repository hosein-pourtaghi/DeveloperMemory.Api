# Security Architecture

**Last verified:** August 29, 2026

---

## Environment-Bound Authentication Model

The API has two deliberately separate runtime modes:

| Environment | Authentication | Authorization | Developer credentials |
|---|---|---|---|
| `Development` | Auth-free developer mode | Existing `[Authorize]` checks are satisfied by a deterministic local identity | None required |
| `Production` and all non-development environments | API key via Bearer token | Enabled | Required |

Normal local development does not require login, JWTs, API keys, or an `Authorization` header. The existing `DevelopmentAuthenticationHandler` supplies a deterministic authenticated identity, so protected controllers and their existing authorization attributes remain in place without creating a separate developer workflow.

This is environment-driven rather than Docker-driven:

- The `Dockerfile` defaults to `ASPNETCORE_ENVIRONMENT=Production`, so a deployed container retains authentication and authorization.
- Docker Compose services explicitly set `ASPNETCORE_ENVIRONMENT=Development` for local development convenience.
- A container behaves according to its `ASPNETCORE_ENVIRONMENT`; Docker itself is not an authentication mode.

### Development flow

```text
ASPNETCORE_ENVIRONMENT=Development
    ↓
DevelopmentOrApiKey policy scheme
    ↓ (no Bearer header)
DevelopmentAuthenticationHandler
    ↓
Deterministic ClaimsPrincipal
    ↓
Existing [Authorize] endpoints execute normally
```

The handler has a second internal environment check and returns no authentication result unless both the host environment is `Development` and the development setting is enabled. The Development configuration enables the setting; the base configuration defaults it to `false`.

If a Bearer token is supplied in Development, the policy scheme forwards to the normal `ApiKey` handler instead. This preserves the real API-key path for development testing.

### Development identity

The development principal uses the same identity claims consumed by `HttpContextCurrentUser` and the Application layer:

- `ClaimTypes.NameIdentifier`: `local-development-owner`
- `ClaimTypes.Name`: `Local Development Owner`
- `development_bypass`: `true`

The owner ID remains server-derived and is used by the normal memory ownership filters. Development convenience does not make memories shared across owners.

### Production flow

```text
ASPNETCORE_ENVIRONMENT=Production (or any non-Development value)
    ↓
Development scheme cannot be selected
    ↓
ApiKeyAuthenticationHandler
    ↓
Valid Bearer API key required for [Authorize] endpoints
```

Production and staging do not select `DevelopmentAuthenticationHandler`. The production path continues to use the existing API-key implementation, including database lookup, configuration fallback, expiration, revocation, rotation, audit events, and authorization behavior.

---

## API-Key Authentication (Production and Explicit API-Key Requests)

**Mechanism:** API Key via Bearer token

Protected endpoints in Production require:
```
Authorization: Bearer <api-key>
```

### Key Storage

| Storage | Purpose | Production? |
|---------|---------|-------------|
| PostgreSQL `ApiKeys` table | Primary key store | Yes |
| `appsettings.json` `Authentication:ApiKeys` | Bootstrap/fallback configuration | Development-oriented |

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
| No credentials → 401 | ✅ | HTTP GET without auth header in Production/non-Development |
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
| Gateway auth → 401 | ✅ | HTTP POST /v1/chat/completions without auth in Production/non-Development |

---

## Memory Ownership Model

### Identity Flow

```
Development identity or API key → ClaimsPrincipal → ICurrentUser → OwnerId
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

| Scenario | Development | Production / non-development |
|---|---|---|
| No credentials on protected endpoint | Authenticated local identity; request proceeds | 401 Unauthorized |
| Invalid API key | Forwarded to API-key handler and rejected | 401 Unauthorized |
| Expired key | Rejected | 401 Unauthorized |
| Revoked key | Rejected | 401 Unauthorized |
| Cross-user access | 404 Not Found | 404 Not Found |
| Rate limited | 429 Too Many Requests | 429 Too Many Requests |

Cross-user access returns 404 to prevent information leakage about resource existence.

## Security Boundary Confirmation

```text
Development auth-free mode: enabled only in Development
Production authentication: enabled
Production authorization: enabled
Docker bypass: none; behavior follows ASPNETCORE_ENVIRONMENT
Existing API contracts: preserved
Memory ownership isolation: preserved
```

---

## Known Limitations

1. **No JWT** — Browser apps need a different auth scheme
2. **InMemory rate limits** — Rate limit counters reset on restart
3. **No key background cleanup** — Expired keys remain until manually cleaned
4. **PostgreSQL ownership** — Runtime verified only via InMemory mode
5. **No integration tests** — Controller endpoints not integration-tested
