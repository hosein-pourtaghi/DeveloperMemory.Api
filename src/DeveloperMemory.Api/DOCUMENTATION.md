# Documentation Index

*Last updated: 2026-08-25*

---

## Documentation Map

| Document | Purpose |
|---|---|
| [README.md](README.md) | Project overview, current capabilities, quick start, API reference |
| [PROJECT_VISION.md](PROJECT_VISION.md) | **Canonical vision**, principles, target architecture, non-goals |
| [CURRENT_STATUS.md](CURRENT_STATUS.md) | Verified implementation inventory based on source code audit |
| [ROADMAP.md](ROADMAP.md) | Development roadmap with accurate implementation tracking |
| [CLAUDE.md](CLAUDE.md) | Complete technical reference: architecture, API, models, configuration |
| [AGENTS.md](AGENTS.md) | AI agent coding guide: standards, architecture rules, extension patterns |
| [KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md) | YAML frontmatter format for knowledge documents and profiles |
| [CHANGELOG.md](CHANGELOG.md) | Version history and design milestones |

---

## Quick Reference

**What is this?** A persistent, intelligent AI memory layer and Memory Intelligence Gateway.

**Current status?** Working .NET 10.0 system with Clean Architecture, PostgreSQL persistence, persistent memory management, OpenAI-compatible gateway, and legacy knowledge/profile support.

**How to run?**
```bash
cd DeveloperMemory.Api
dotnet restore
dotnet run
```
Requires .NET 10.0 SDK and PostgreSQL (or in-memory mode).

**What's the vision?** See [PROJECT_VISION.md](PROJECT_VISION.md) for the full vision — a modular, cloud-first, provider-independent Memory Intelligence Gateway.

**What's implemented?** See [CURRENT_STATUS.md](CURRENT_STATUS.md) for an honest assessment based on source code verification.

**What's next?** See [ROADMAP.md](ROADMAP.md) for the development roadmap.

**Want to implement something?** See [AGENTS.md](AGENTS.md) for coding standards and [CLAUDE.md](CLAUDE.md) for the technical specification.

---

## Documentation Hierarchy

```
PROJECT_VISION.md          ← Canonical identity, vision, principles
    │
    ├── README.md           ← Public-facing overview
    ├── CURRENT_STATUS.md   ← Verified implementation reality
    ├── ROADMAP.md          ← Future evolution
    │
    ├── CLAUDE.md           ← Technical reference
    ├── AGENTS.md           ← Coding guide
    ├── KNOWLEDGE_FORMAT.md ← Format reference
    │
    └── CHANGELOG.md        ← History
```

The canonical hierarchy ensures no competing project identities. PROJECT_VISION.md is the single source of truth for identity and direction. Source code is the single source of truth for current implementation.
