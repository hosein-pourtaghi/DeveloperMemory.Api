# Developer Memory API - Project Summary

## Project Overview
The Developer Memory API is a .NET 10.0 Web API that serves as a knowledge management and AI assistant gateway. It enables developers to store, search, and retrieve technical knowledge while leveraging AI assistants to query that knowledge with contextual awareness.

## Documentation Index
- [README](README.md) - Quick start and overview
- [Documentation](DOCUMENTATION.md) - Architectural overview
- [API Specification](API_SPECIFICATION.md) - Detailed endpoint definitions
- [Data Models](DATA_MODELS.md) - Complete model schemas
- [Configuration](CONFIGURATION.md) - Configuration reference
- [Error Handling](ERROR_HANDLING.md) - Troubleshooting guide

## Core Purpose
- **Knowledge Management**: Store and search technical documentation in Markdown format
- **Developer Profiles**: Manage developer profiles with skills, experience, and roles
- **AI Assistant Gateway**: Proxy requests to external LLM APIs with contextual information from profiles and documents

## Key Components

### 1. Controllers (API Layer)
- **KnowledgeController**: Handles document search, retrieval, and reindexing
- **ProfilesController**: Manages developer profile operations
- **ProxyController**: The main AI assistant gateway that combines context and forwards requests

### 2. Services (Business Logic)
- **KnowledgeService**: Document parsing, searching, and indexing with relevance scoring
- **ProfileService**: Profile parsing and metadata extraction from Markdown files
- **FreeLlmApiClient**: HTTP client for communicating with external LLM APIs
- **PromptBuilder**: Constructs comprehensive prompts using profiles and search results

### 3. Models (Data Structures)
- **DeveloperProfile**: Contains developer information (name, role, skills, experience, bio)
- **KnowledgeDocument**: Contains technical documentation (title, content, project, tags)
- **PromptRequest**: API request model for AI queries
- **SearchResult**: Search results with relevance scores

## Technology Stack
- **Framework**: .NET 10.0 with ASP.NET Core
- **Logging**: Serilog
- **API Documentation**: Swashbuckle (OpenAPI)
- **HTTP Client**: HttpClient for external API calls
- **Serialization**: JSON.NET
- **File Format**: Markdown with YAML frontmatter

## Data Storage
- **Knowledge Documents**: Stored as Markdown files in `Paths:KnowledgeFolder` with YAML frontmatter for metadata
- **Developer Profiles**: Stored as Markdown files in `Paths:ProfilesFolder` with YAML frontmatter for metadata

## API Endpoints
- `GET /api/Knowledge`: Search documents by query, project, and tags
- `GET /api/Knowledge/documents`: Retrieve all documents
- `POST /api/Knowledge/reindex`: Reindex all documents
- `GET /api/Knowledge/{id}`: Get specific document by ID
- `GET /api/Profiles`: Get all developer profiles
- `POST /api/Profiles`: Load profile from file
- `POST /api/Proxy`: Forward request to LLM API with context

## Frontmatter Format
Both documents and profiles use YAML frontmatter for metadata:
```yaml
---
title: "Document Title"
project: "ProjectName"
tags: ["tag1", "tag2", "tag3"]
---
Document content here...

---
name: "Developer Name"
role: "Senior Developer"
skills: ["C#", "ASP.NET", "Docker"]
experience: "10 years"
---
Developer bio here...
```

## Usage Patterns
1. **Document Search**: Query technical documentation with optional project and tag filters
2. **Profile Loading**: Load developer profiles from Markdown files
3. **AI Assistant Queries**: Send queries to LLM APIs with contextual information from profiles and relevant documents

## Configuration
- `appsettings.json`: Contains paths to knowledge/profiles folders and LLM API settings
- `launchSettings.json`: Development environment configuration

## Development Setup
1. `dotnet restore` - Install dependencies
2. `dotnet run` - Run the application
3. API available at `https://localhost:7277` with Swagger UI at `/swagger`

## Future Enhancements
- More sophisticated search algorithms (TF-IDF, vector search)
- Authentication and authorization
- Support for multiple LLM providers
- Caching for search results
- Comprehensive error handling and logging