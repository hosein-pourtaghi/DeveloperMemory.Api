# Developer Memory API

This is a .NET API that provides a knowledge management and AI assistant gateway.

## Overview

The Developer Memory API is a comprehensive system designed to help developers manage their technical knowledge and use AI assistants to query that knowledge. It provides:

1. **Knowledge Management** - Store and search technical documents
2. **Developer Profiles** - Store and manage developer profiles
3. **AI Assistant Gateway** - Proxy requests to external LLM APIs with context

## Architecture

The API follows a clean separation of concerns with the following components:

### 1. Controllers
- `KnowledgeController`: Handles document search, retrieval, and reindexing
- `ProfilesController`: Manages developer profiles
- `ProxyController`: The main AI assistant gateway

### 2. Services
- `KnowledgeService`: Handles document loading, searching, and indexing
- `ProfileService`: Manages developer profiles
- `FreeLlmApiClient`: Interfaces with external LLM APIs
- `PromptBuilder`: Constructs prompts with context from profiles and documents

### 3. Models
- `DeveloperProfile`: Represents developer profiles with skills, experience, etc.
- `KnowledgeDocument`: Represents technical documents with metadata
- `PromptRequest`: Request model for AI assistant queries
- `SearchResult`: Search results with relevance scores
- Configuration models for API settings

### 4. Infrastructure
- Configuration services for app settings
- Service collection extensions for dependency injection

## Key Features

### Knowledge Management
- **Document Storage**: Markdown files stored in configured folders
- **Frontmatter Support**: Documents can have metadata in YAML frontmatter
- **Search**: Full-text search with relevance scoring
- **Reindexing**: Automatic document indexing on startup

### Developer Profiles
- **Profile Storage**: Developer information stored as markdown files
- **Profile Parsing**: Extracts metadata from frontmatter
- **Profile Management**: Load and retrieve profiles

### AI Assistant Gateway
- **Context Integration**: Combines developer profiles and documents as context
- **Prompt Construction**: Builds comprehensive prompts with relevant information
- **API Proxy**: Interfaces with external LLM APIs
- **Error Handling**: Robust error handling and status checking

## Configuration

The API uses configuration files to manage settings:

### appsettings.json
- `Paths:KnowledgeFolder`: Path to store knowledge documents
- `Paths:ProfilesFolder`: Path to store developer profiles
- `FreeLlmApi`: Configuration for external LLM API

## API Endpoints

### Knowledge Controller
- `GET /api/Knowledge`: Search documents by query, project, and tags
- `GET /api/Knowledge/documents`: Get all documents
- `POST /api/Knowledge/reindex`: Reindex all documents
- `GET /api/Knowledge/{id}`: Get a specific document by ID

### Profiles Controller
- `GET /api/Profiles`: Get all developer profiles
- `POST /api/Profiles`: Load a developer profile from file

### Proxy Controller
- `POST /api/Proxy`: Forward request to LLM API with context

## Frontmatter Format

Both knowledge documents and developer profiles use YAML frontmatter:

```yaml
# Knowledge Document Example
---
title: "ASP.NET Core Configuration"
project: "DeveloperMemory"
tags: ["aspnet", "configuration", "settings"]
---
This document explains how to configure ASP.NET Core applications...
```

```yaml
# Developer Profile Example
---
name: "John Developer"
role: "Senior Software Engineer"
skills: ["C#", "ASP.NET", "Docker", "Azure"]
experience: "10 years"
---
John has extensive experience with enterprise applications and cloud solutions.
```

## Usage Example

### Search Documents
```http
GET /api/Knowledge?query=aspnet&tags=configuration
```

### Get Developer Profile
```http
POST /api/Profiles
Content-Type: text/plain
Content-Disposition: attachment; filename="developer-profile.md"

/path/to/developer-profile.md
```

### AI Assistant Query
```http
POST /api/Proxy
Content-Type: application/json

{
  "query": "Explain dependency injection in ASP.NET Core",
  "profileId": "profile-id",
  "systemPrompt": "You are a technical expert explaining ASP.NET Core concepts"
}
```

## Technology Stack

- **.NET 10.0** with ASP.NET Core
- **Serilog** for logging
- **Swashbuckle** for OpenAPI documentation
- **HttpClient** for external API calls
- **JSON.NET** for serialization

## Development

To run the application:

1. Install dependencies:
   ```
   dotnet restore
   ```

2. Run the application:
   ```
   dotnet run
   ```

The API will be available at `https://localhost:7277` (by default) with Swagger UI for documentation.

## Future Enhancements

1. Add more sophisticated search algorithms (e.g., TF-IDF, vector search)
2. Implement authentication and authorization
3. Add support for multiple LLM providers
4. Implement caching for search results
5. Add more comprehensive error handling and logging
```
<task_progress>
- [x] Read Program.cs (entry point)
- [x] Read .csproj (dependencies)
- [x] Read all controllers
- [x] Read all services
- [x] Read all models
- [x] Read infrastructure files
- [x] Create comprehensive README.md
</task_progress>
</write_to_file>