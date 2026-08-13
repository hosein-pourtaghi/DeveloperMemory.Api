# Developer Memory API Documentation

## Overview
The Developer Memory API is a .NET 10.0 Web API designed as a knowledge management and AI assistant gateway. It provides three core functionalities:
1. **Knowledge Management**: Store, search, and manage technical documents
2. **Developer Profiles**: Store and manage developer profiles
3. **AI Assistant Gateway**: Proxy requests to external LLM APIs with contextual information

## Architecture
The API follows a clean separation of concerns with these key components:
- **Controllers**: Handle HTTP requests
  - `KnowledgeController`: Document search and retrieval
  - `ProfilesController`: Developer profile management
  - `ProxyController`: AI assistant gateway
- **Services**: Business logic implementation
  - `KnowledgeService`: Document loading, searching, and indexing
  - `ProfileService`: Profile loading and parsing
  - `FreeLlmApiClient`: Interface to external LLM APIs
  - `PromptBuilder`: Constructs prompts with context
- **Models**: Data structures
  - `DeveloperProfile`: Represents developer profiles
  - `KnowledgeDocument`: Represents technical documents
  - `PromptRequest`: API request model for AI queries
  - `SearchResult`: Search results with relevance scores

## Key Features
### Knowledge Management
- Document storage in Markdown format with YAML frontmatter
- Full-text search with relevance scoring
- Automatic reindexing on startup
- Project and tag-based filtering

### Developer Profiles
- Profile storage in Markdown format
- Metadata extraction from frontmatter
- Skill and experience tracking

### AI Assistant Gateway
- Context integration from profiles and documents
- Prompt construction with relevant information
- Proxy to external LLM APIs
- Error handling and status checking

## Configuration
The API uses configuration files to manage settings:
- `appsettings.json`: Paths to knowledge and profiles folders, LLM API configuration
- `launchSettings.json`: Development environment settings

## API Endpoints
### Knowledge Controller
- `GET /api/Knowledge`: Search documents by query, project, and tags
- `GET /api/Knowledge/documents`: Get all documents
- `POST /api/Knowledge/reindex`: Reindex all documents
- `GET /api/Knowledge/{id}`: Get specific document by ID

### Profiles Controller
- `GET /api/Profiles`: Get all developer profiles
- `POST /api/Profiles`: Load profile from file

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
- .NET 10.0 with ASP.NET Core
- Serilog for logging
- Swashbuckle for OpenAPI documentation
- HttpClient for external API calls
- JSON.NET for serialization

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
The API will be available at `https://localhost:7277` with Swagger UI for documentation.