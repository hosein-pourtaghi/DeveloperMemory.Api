# Developer Memory API Documentation

## Overview

The Developer Memory API is a .NET 10.0 Web API that serves as a knowledge management and AI assistant gateway. It enables developers to store, search, and retrieve technical knowledge while leveraging AI assistants to query that knowledge with contextual awareness.

## Architecture

This system follows a layered architecture:

### Layers

1. **Presentation Layer**
   - RESTful API endpoints exposed via ASP.NET Core
   - Swagger UI for interactive documentation and testing

2. **Application Layer**
   - Controllers (`KnowledgeController`, `ProfilesController`, `ProxyController`)
   - Services (`KnowledgeService`, `ProfileService`, `FreeLlmApiClient`, `PromptBuilder`)
   - Business logic orchestration

3. **Domain Layer**
   - Data models (`DeveloperProfile`, `KnowledgeDocument`, `PromptRequest`, `SearchResult`)
   - YAML frontmatter parsing and validation

4. **Infrastructure Layer**
   - Configuration management (`appsettings.json`)
   - External LLM API integration
   - Logging (Serilog)

### Data Flow

1. **Document Upload/Creation**
   - Markdown files with YAML frontmatter are placed in `Paths:KnowledgeFolder`
   - `KnowledgeService` parses files and creates `KnowledgeDocument` objects
   - Documents are indexed for search

2. **Profile Management**
   - Markdown files with YAML frontmatter are placed in `Paths:ProfilesFolder`
   - `ProfileService` parses files and creates `DeveloperProfile` objects

3. **Query Processing**
   - `ProxyController` receives AI queries via `PromptRequest`
   - Combines relevant documents and profiles from the database
   - Constructs a prompt using `PromptBuilder`
   - Sends the prompt to the external LLM API
   - Returns the LLM response

4. **Indexing**
   - Automatic reindexing triggered via `POST /api/Knowledge/reindex`
   - Maintains search index with relevance scoring

### Key Components

- **KnowledgeController**: Handles document search, retrieval, and reindexing
- **ProfilesController**: Manages developer profile operations
- **ProxyController**: Main AI assistant gateway combining context and forwarding requests
- **KnowledgeService**: Document parsing, searching, and indexing with relevance scoring
- **ProfileService**: Profile parsing and metadata extraction from Markdown files
- **FreeLlmApiClient**: HTTP client for communicating with external LLM APIs
- **PromptBuilder**: Constructs comprehensive prompts using profiles and search results