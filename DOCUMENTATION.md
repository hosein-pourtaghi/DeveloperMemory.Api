# Documentation Index

*Last updated: 2026-08-27*
*Reflects: Build-verified, runtime-tested state*

---

## Documentation Map

| Document | Purpose | Status |
|---|---|---|
| [README.md](README.md) | Project overview, current capabilities, quick start, API reference | Needs update |
| [PROJECT_VISION.md](PROJECT_VISION.md) | **Canonical vision**, principles, target architecture, non-goals | Accurate |
| [CURRENT_STATUS.md](CURRENT_STATUS.md) | Verified implementation inventory | ✅ Updated 2026-08-27 |
| [ROADMAP.md](ROADMAP.md) | Development roadmap with accurate implementation tracking | ✅ Updated 2026-08-27 |
| [CLAUDE.md](CLAUDE.md) | Complete technical reference: architecture, API, models, configuration | Needs update |
| [AGENTS.md](AGENTS.md) | AI agent coding guide: standards, architecture rules, extension patterns | Needs update |
| [KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md) | YAML frontmatter format for knowledge documents and profiles | Accurate |
| [CHANGELOG.md](CHANGELOG.md) | Version history and design milestones | Needs update |
| [docs/ARCHITECTURE_AUDIT.md](docs/ARCHITECTURE_AUDIT.md) | Architecture audit, gap analysis, target architecture direction | Superseded |
| [docs/ARCHITECTURE_VERIFICATION.md](docs/ARCHITECTURE_VERIFICATION.md) | Build/test/runtime verified architecture report | ✅ Created 2026-08-27 |

---

## Verification Summary

| Metric | Value |
|---|---|
| Build | ✅ 0 errors, 18 warnings (NuGet advisories) |
| Tests | ✅ 140 passed, 0 failed, 0 skipped |
| Runtime | ✅ All major endpoints verified |
| DI | ✅ All lifetime issues fixed |
| Persistence | ✅ In-memory fallback works; PostgreSQL optional |

---

## Quick Reference

**What is this?** A persistent, intelligent AI memory layer and Memory Intelligence Gateway.

**Current status?** Working .NET 10.0 system with Clean Architecture, PostgreSQL persistence, 6-stage Prompt Intelligence Engine, memory intelligence pipeline, multi-mode retrieval, embedding infrastructure, OpenAI-compatible gateway, 140 passing tests, and runtime-verified endpoints.

**How to run?**

Without Docker (uses InMemory when PostgreSQL unavailable):
```bash
dotnet restore
dotnet run --project src/DeveloperMemory.Api
```

With Docker:
```bash
docker compose up api          # in-memory mode
docker compose up api-postgres  # PostgreSQL mode
```

**How to test?**
```bash
dotnet test
```

**What's the vision?** See [PROJECT_VISION.md](PROJECT_VISION.md) — a modular, cloud-first, provider-independent Memory Intelligence Gateway.

**What's implemented?** See [CURRENT_STATUS.md](CURRENT_STATUS.md) — verified against source code, build, and runtime.

**Architecture verification?** See [docs/ARCHITECTURE_VERIFICATION.md](docs/ARCHITECTURE_VERIFICATION.md) — build, test, and runtime verification report.

**What's next?** See [ROADMAP.md](ROADMAP.md) — authentication, authorization, production hardening.

---

## Documentation Hierarchy

```
PROJECT_VISION.md              ← Canonical identity, vision, principles
    │
    ├── README.md               ← Public-facing overview
    ├── CURRENT_STATUS.md       ← Verified implementation reality (2026-08-27)
    ├── ROADMAP.md              ← Future evolution (2026-08-27)
    │
    ├── CLAUDE.md               ← Technical reference
    ├── AGENTS.md               ← Coding guide
    ├── KNOWLEDGE_FORMAT.md     ← Format reference
    │
    ├── docs/
    │   ├── ARCHITECTURE_VERIFICATION.md  ← Build/test/runtime verification (2026-08-27)
    │   └── ARCHITECTURE_AUDIT.md         ← Superseded by verification report
    │
    └── CHANGELOG.md            ← History
```
