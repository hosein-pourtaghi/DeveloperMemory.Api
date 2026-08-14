# Developer Memory API Documentation

## Indexing Lifecycle

The indexing lifecycle in Developer Memory API involves the following steps:

1. **File Discovery**
   - Scans the `KnowledgeFolder` directory for Markdown files
   - Creates new documents for each found file

2. **Parsing**
   - Reads file content and extracts YAML frontmatter
   - Parses metadata (title, project, tags)
   - Extracts main content from Markdown

3. **Indexing**
   - Stores documents in memory as KnowledgeDocument objects
   - Creates search index with relevance scoring
   - Updates document metadata (last modified timestamp)

4. **Search Processing**
   - Matches queries against title, content, project, and tags
   - Calculates relevance score based on:
     - Title match (0.5 points)
     - Content match (0.3 points)
     - Project match (0.1 points)
     - Tag matches (0.1 points per match)

5. **Reindexing**
   - Triggered via POST /api/Knowledge/reindex
   - Replaces current document list with newly loaded documents
   - Maintains search index consistency

6. **Caching**
   - Maintains in-memory cache of documents
   - Updates cache on reindex or document modification

## Knowledge Format Guide

### YAML Frontmatter Requirements

Both knowledge documents and developer profiles must use YAML frontmatter with the following structure:

```yaml
---
title: "Document Title"
project: "ProjectName"
tags: ["tag1", "tag2", "tag3"]
---
Document content here...
```

For developer profiles:

```yaml
---
name: "Developer Name"
role: "Senior Developer"
skills: ["C#", "ASP.NET"]
experience: "10 years"
---
Developer bio here...
```

### Metadata Fields

**Knowledge Documents:**
- `title` (required): Document title
- `project` (optional): Associated project name
- `tags` (optional): List of categorization tags

**Developer Profiles:**
- `name` (required): Full name
- `role` (required): Professional role
- `skills` (required): List of technical skills
- `experience` (required): Years or description of experience

## Architecture Diagram

[Mermaid.js diagram would be inserted here showing controllers, services, models, and data flow]