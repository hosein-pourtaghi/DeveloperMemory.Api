# Developer Memory API Documentation

This document provides a high-level overview and architectural guide for the Developer Memory API. For detailed technical specifications, please refer to the following documents:

- [API Specification](API_SPECIFICATION.md)
- [Data Models](DATA_MODELS.md)
- [Configuration](CONFIGURATION.md)
- [Error Handling](ERROR_HANDLING.md)

## Overview
The Developer Memory API is a .NET 10.0 Web API designed as a knowledge management and AI assistant gateway. It provides three core functionalities:
1. **Knowledge Management**: Store, search, and manage technical documents.
2. **Developer Profiles**: Store and manage developer profiles.
3. **AI Assistant Gateway**: Proxy requests to external LLM APIs with contextual information.

## Architecture
The API follows a clean separation of concerns:

### Controllers
- `KnowledgeController`: Handles document search, retrieval, and reindexing.
- `ProfilesController`: Manages developer profile operations.
- `ProxyController`: The main AI assistant gateway that combines context and forwards requests.

### Services
- `KnowledgeService`: Document parsing, searching, and indexing with relevance scoring.
- `ProfileService`: Profile parsing and metadata extraction from Markdown files.
- `FreeLlmApiClient`: HTTP client for communicating with external LLM APIs.
- `PromptBuilder`: Constructs comprehensive prompts using profiles and search results.

## Data Storage
- **Knowledge Documents**: Stored as Markdown files in the configured `KnowledgeFolder` with YAML frontmatter.
- **Developer Profiles**: Stored as Markdown files in the configured `ProfilesFolder` with YAML frontmatter.

## Frontmatter Format
Both documents and profiles use YAML frontmatter for metadata:

```yaml
# Knowledge Document Example
---
title: "Document Title"
project: "ProjectName"
tags: ["tag1", "tag2"]
---
Document content here...
```

```yaml
# Developer Profile Example
---
name: "Developer Name"
role: "Senior Developer"
skills: ["C#", "ASP.NET"]
experience: "10 years"
---
Developer bio here...