---
name: state-audit
description: Reconcile documentation claims against verified source before any status or completion report. Use before claiming a phase/feature is complete, or when historical reports cite test numbers.
---

# State audit — docs vs. source

**Iron rule (AGENTS.md): source code is the primary truth. Design documents may lag.**
Historical conversation claims and phase reports are *not* evidence of completion.

## The failure mode this skill prevents

This repo previously carried docs claiming 598 tests when the suite was 1,014, a
"5-project test layout" that was actually 4 projects, and phase labels (V2-2/3/4,
"Dynamic Agent System") for code that does not exist. Verify everything.

## Checklist

1. **Numbers** — never copy from docs. Discover:
   ```bash
   dotnet test DeveloperMemory.Api.sln --no-build  # per-project Passed counts
   ```
2. **Components** — for every claimed feature, locate the implementation:
   ```bash
   grep -rn "class <Component>" src/            # does the class exist?
   grep -n "<Interface>" src/DeveloperMemory.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs
   ```
   No class + no DI registration = **not implemented**, whatever the docs say.
   Registered-but-unconsumed (check who injects the interface) = implemented but **unreachable** — report both facts separately.
3. **Report format** — distinguish explicitly:
   - *static inspection* (grep/read) vs. *executed* (build/test output)
   - implemented vs. unreachable vs. non-existent
4. **Completion claims** — require execution evidence for the specific claim. "A previous prompt said it was done" is not evidence.
5. **After verifying**, update `CURRENT_STATUS.md`/`ROADMAP.md` to match reality — stale docs here have repeatedly misled future sessions.

## Ground truth (verified 2026-09-09)

1,014 tests (Domain 38 / Application 597 / Api 267 / Infrastructure 112); 4 test
projects; AgentContext = Phase T/W work; no "V2-2/V2-3/V2-4" exists.
See CURRENT_STATUS.md "Known Non-Features".
