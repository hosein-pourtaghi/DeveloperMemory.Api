# Knowledge and Profile Format — YAML Frontmatter Reference

Both knowledge documents and developer profiles use Markdown files with YAML frontmatter. The parser extracts metadata from the frontmatter block and uses the remaining content as the body.

**Important:** The frontmatter format below reflects the actual files currently in the repository. The planned implementation may evolve this format — see the notes at the end of each section.

---

## Knowledge Document Format

**Location:** `Knowledge/` directory

```markdown
---
name: "AI Agent Rules"
scope: global
---

# AI Agent Rules

How I expect an AI coding agent to behave when working on any of my projects.

## Understand Before Changing
Before modifying any code, inspect the relevant project structure...
```

### Supported Frontmatter Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | string | No | Document name. Falls back to filename if omitted. |
| `scope` | string | No | Scope of the document (e.g., `global`, or a project name). |

### Important Notes

- The frontmatter parser splits on `:` and uses the **first two segments only**. Values containing `:` may be truncated.
- Files without valid frontmatter (missing `---` delimiters) are still loaded — `name` defaults to the filename, `scope` is empty.
- Only `.md` files are loaded.
- **Planned evolution:** Future versions may add `title`, `project`, and `tags` fields for richer filtering and retrieval. The current format is intentionally minimal.

### Example Documents in This Repository

| File | Name | Scope |
|---|---|---|
| `ai-agent-rules.md` | AI Agent Rules | global |
| `code-generation-rules.md` | Code Generation Rules | global |

---

## Developer Profile Format

**Location:** `Profiles/` directory

```markdown
---
name: Developer
scope: global
role: Full-Stack Developer
experience: 5+ years
---

# Developer Profile

Full-stack developer with deep expertise in the .NET ecosystem...
```

### Supported Frontmatter Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | string | No | Developer's name or profile name |
| `scope` | string | No | Scope of the profile (e.g., `global`, or a project name) |
| `role` | string | No | Professional role/title |
| `experience` | string | No | Years or description of experience |

### Important Notes

- The body (after frontmatter) becomes the profile's bio/description.
- Files without valid frontmatter (missing `---` delimiters) are skipped and return `null`.
- Only `.md` files are loaded.
- **Planned evolution:** Future versions may add `skills` field for comma-separated skill lists. The current format is intentionally minimal.

### Example Profiles in This Repository

| File | Name | Role | Experience |
|---|---|---|---|
| `developer-profile.md` | Developer | Full-Stack Developer | 5+ years |
| `development-preferences.md` | Development Preferences | — | — |

---

## Relevance Scoring (Planned)

The planned retrieval algorithm uses keyword matching to score document relevance:

| Match Location | Score |
|---|---|
| Name contains query | +0.5 |
| Content contains query | +0.3 |
| Scope contains query | +0.1 |

Results are sorted by score in descending order. This scoring is a design specification, not yet implemented.

---

## Design Notes

The current frontmatter format is intentionally simple. As the project evolves:

1. **v1 target:** Add `title`, `project`, and `tags` fields to knowledge documents for richer filtering.
2. **v1 target:** Add `skills` field to profiles for skill-based retrieval.
3. **v2+:** Consider embedding generation from document content for semantic search.
4. **v2+:** Consider structured metadata (JSON) alongside Markdown for programmatic access.

The format should evolve based on actual usage patterns, not upfront design speculation.
