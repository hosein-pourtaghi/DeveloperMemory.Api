# Knowledge Format — YAML Frontmatter Reference

Both knowledge documents and developer profiles use Markdown files with YAML frontmatter. The parser extracts metadata from the frontmatter block and uses the remaining content as the body.

## Document Format

**Location:** `Knowledge/` directory (configurable via `AppSettings:Paths:KnowledgeFolder`)

```markdown
---
name: "How to Configure Serilog"
project: "MyApp"
tags: logging, dotnet, configuration
---

# How to Configure Serilog

## Installation

Install the NuGet package...

## Configuration

Add to `Program.cs`...
```

### Supported Frontmatter Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | string | No | Document title. Falls back to filename if omitted. Also accepts `title` as an alias. |
| `project` | string | No | Project name for filtering. Empty string if omitted. |
| `tags` | string | No | Comma-separated tags (e.g., `logging, dotnet`). Empty list if omitted. |

### Important Notes

- The `name` and `title` fields are aliases — either works. The actual knowledge files in this repository use `name:`.
- Tags are a **single comma-separated string**, not a YAML array: `tags: logging, dotnet` ✅ — `tags: [logging, dotnet]` ❌
- The frontmatter parser splits on `:` and uses the **first two segments only**. Values containing `:` will be truncated.
- Files without valid frontmatter are still loaded — `title` defaults to the filename, `project` and `tags` are empty.
- Only `.md` files are loaded.

---

## Profile Format

**Location:** `Profiles/` directory (configurable via `AppSettings:Paths:ProfilesFolder`)

```markdown
---
name: Jane Smith
role: Senior Backend Developer
skills: C#, ASP.NET Core, Docker, PostgreSQL
experience: 8 years
---

# Jane Smith

Senior backend developer specializing in .NET ecosystem...
```

### Supported Frontmatter Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | string | No | Developer's name |
| `role` | string | No | Professional role/title |
| `skills` | string | No | Comma-separated skills list |
| `experience` | string | No | Years or description of experience |

### Important Notes

- Skills are a **single comma-separated string**: `skills: C#, Docker, Git` ✅
- The body (after frontmatter) becomes the `Bio` field
- Files without valid frontmatter (missing `---` delimiters) are skipped and return `null`
- Only `.md` files are loaded

---

## Relevance Scoring

When searching documents, relevance is calculated using keyword matching:

| Match Location | Score |
|---|---|
| Title contains query | +0.5 |
| Content contains query | +0.3 |
| Project contains query | +0.1 |
| Each matching tag | +0.1 |

Results are sorted by score in descending order.
