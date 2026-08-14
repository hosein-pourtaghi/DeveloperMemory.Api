# Knowledge Format

## Document Structure

All knowledge documents are stored as Markdown files with YAML frontmatter at the top of the file.

### YAML Frontmatter

```yaml
---
title: "Document Title"
description: "Brief description of the document"
category: "Category Name"
tags: ["tag1", "tag2", "tag3"]
author: "Author Name"
created_at: "2026-01-01T00:00:00Z"
updated_at: "2026-01-01T00:00:00Z"
---
```

### Required Fields

| Field | Type | Description |
|-------|------|-------------|
| `title` | string | Unique title of the document |
| `description` | string | Brief summary of the document content |
| `category` | string | Category classification |
| `tags` | array of strings | Relevant tags for indexing and filtering |
| `author` | string | Author of the document |

### Example

```markdown
---
title: "Developer Profile Template"
description: "Template for creating developer profiles"
category: "Profiles"
tags: ["developer", "profile", "template"]
author: "System Admin"
created_at: "2026-01-15T10:30:00Z"
updated_at: "2026-06-01T14:20:00Z"
---

# Developer Profile

## Overview
This is a template for creating developer profiles...

## Skills
- Language: Python
- Framework: .NET
- Experience: 5 years

## Contact Information
- Email: developer@example.com
- Location: San Francisco, CA
```

## Indexing

Documents are automatically indexed for search using the `tags` and `category` fields. The `title` and `description` are also used for relevance scoring.