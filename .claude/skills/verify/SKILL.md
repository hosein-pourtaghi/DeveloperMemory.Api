---
name: verify
description: Build the solution and run the full 1,014-test suite, including the PostgreSQL-backed integration tests. Use whenever changes need verification before reporting completion.
---

# Verify: build + full test suite

## Environment (this workspace has no system-wide dotnet)

```bash
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 PATH="$HOME/.dotnet:$PATH"
```

- The SDK lives at `~/.dotnet` (10.0.401). The invariant-globalization flag is required: slim images lack ICU and `dotnet` core-dumps without it.

## Build

```bash
dotnet build DeveloperMemory.Api.sln
```

Expected: **0 errors**. ~683 warnings are pre-existing (NuGet advisories NU1903 on `System.Security.Cryptography.Xml 9.0.0`, xUnit1031 blocking-task warnings in tests, CS8602/CS0168 in Program.cs). Do not chase them.

## Test

```bash
dotnet test DeveloperMemory.Api.sln --configuration Debug --no-build
```

Expected: **1,014 passed / 0 failed** (Domain 38, Application 597, Api 267, Infrastructure 112).

- PostgreSQL-backed suites fail **fast** with `NpgsqlException: Failed to connect to 127.0.0.1:5432` when no local Postgres is running. That is environmental, not a regression — see the `/setup-postgres-tests` skill.
- Api.Tests can drop 1–2 tests when the full solution runs in parallel (known flaky-under-parallelism, noted in PHASE_W_REPORT.md). Confirm by re-running `dotnet test tests/DeveloperMemory.Api.Tests/` alone. Never weaken or delete tests to go green.
- Fast loop on one project: `dotnet test tests/DeveloperMemory.Application.Tests/ --no-build`
