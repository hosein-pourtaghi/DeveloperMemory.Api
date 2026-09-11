---
name: setup-postgres-tests
description: Provision a native local PostgreSQL (no Docker) with the role and databases the Postgres-backed test suites require. Use when Postgres tests fail with connection errors or before first test run.
---

# Local PostgreSQL for tests (no Docker)

Project constraint: **no Docker**. Use a native server.

## Install & start (Ubuntu)

```bash
apt-get update && apt-get install -y postgresql postgresql-contrib
service postgresql start
```

## Provision (matches test conventions exactly)

```bash
su - postgres -c "psql -c \"CREATE ROLE developer LOGIN PASSWORD 'devpassword' SUPERUSER;\""
su - postgres -c "psql -c 'CREATE DATABASE developermemory OWNER developer;'"
su - postgres -c "psql -c 'CREATE DATABASE developermemory_test OWNER developer;'"
```

## Why each exists

- `developermemory` — default app connection string; used by `tests/PhaseU_E2E_Verification.sh`.
- `developermemory_test` — Infrastructure `PostgresDbFixture` runs `EnsureDeleted` + `Migrate` per test class, but the **database itself must already exist** (it does not self-create).
- Api.Tests `PostgresE2EFactory` creates and drops its own unique `e2e_*` databases per fixture — no setup needed.

## Verify

```bash
PGPASSWORD=devpassword psql -h 127.0.0.1 -U developer -d developermemory -c "SELECT 1;"
```

Override: the fixture reads `DEVELOPERMEMORY_TEST_CONNECTION` (env var) before falling back to the default connection string.
