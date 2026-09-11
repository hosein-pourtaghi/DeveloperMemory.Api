---
name: runtime-verify
description: Boot the real API and verify live HTTP behavior (health, auth, memory CRUD, gateway) against PostgreSQL or InMemory. Use for deployment checks or when unit tests are not enough.
---

# Runtime verification (real app, real HTTP)

Unit tests prove logic; this proves the running system. Two options, no Docker.

## Option A — integration tests (preferred, automated)

`PostgresE2EFactory` (tests/DeveloperMemory.Api.Tests/PostgresE2ETests.cs) already boots
the real `Program.cs` via `WebApplicationFactory` against unique real `e2e_*`
Postgres databases — 267 Api tests include the full HTTP stack. If Postgres tests
pass (see /setup-postgres-tests), runtime behavior is covered.

## Option B — manual Kestrel boot

The sandbox **kills background processes when the launching tool call ends**, so
long-lived `dotnet run` doesn't survive. Use the repo's own script, which starts
the API and probes it within one shell:

```bash
sh ./tests/PhaseU_E2E_Verification.sh
```

Prereqs: PostgreSQL provisioned (/setup-postgres-tests), database `developermemory`
exists (script drops/recreates it), dotnet on PATH with
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.

Manual single checks once the API is up on `http://127.0.0.1:5041` (Development env:
auth-free):

```bash
curl http://127.0.0.1:5041/health            # {"status":"Healthy","database":"Connected"}
curl -X POST http://127.0.0.1:5041/api/Memory -H 'Content-Type: application/json' \
  -d '{"title":"t","content":"c","scope":"Global"}'
curl http://127.0.0.1:5041/api/Memory/stats
curl http://127.0.0.1:5041/v1/models
```

Production-mode check (API-key auth, no dev bypass): run with
`ASPNETCORE_ENVIRONMENT=Production` and expect 401 without `Authorization: Bearer <key>`.
Never edit `Program.cs` port bindings to test — launchSettings covers 5041 locally,
`PORT` env covers container deployments.
