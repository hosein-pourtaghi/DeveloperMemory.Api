# API Specification

This document provides a detailed specification of the Developer Memory API endpoints.

## Base URL
`https://localhost:7277`

## Knowledge Controller (`/api/Knowledge`)

### Search Documents
`GET /api/Knowledge`
- **Description**: Search documents by query, project, and tags.
- **Parameters**:
  - `query` (string, optional): Search term.
  - `project` (string, optional): Filter by project.
  - `tags` (string, optional): Comma-separated list of tags.
- **Response**: `200 OK` - List of `SearchResult` objects.

### Get All Documents
`GET /api/Knowledge/documents`
- **Description**: Retrieve all stored documents.
- **Response**: `200 OK` - List of `KnowledgeDocument` objects.

### Reindex Documents
`POST /api/Knowledge/reindex`
- **Description**: Trigger a reindexing of all documents.
- **Response**: `200 OK` - Success message.

### Get Document by ID
`GET /api/Knowledge/{id}`
- **Description**: Retrieve a specific document.
- **Response**: `200 OK` - `KnowledgeDocument` object.

## Profiles Controller (`/api/Profiles`)

### Get All Profiles
`GET /api/Profiles`
- **Description**: Retrieve all developer profiles.
- **Response**: `200 OK` - List of `DeveloperProfile` objects.

### Load Profile
`POST /api/Profiles`
- **Description**: Load a profile from a file path.
- **Body**: `text/plain` (file path).
- **Response**: `200 OK` - Success message.

## Proxy Controller (`/api/Proxy`)

### AI Assistant Query
`POST /api/Proxy`
- **Description**: Forward a query to the LLM API with context.
- **Body**: `PromptRequest` (JSON).
- **Response**: `200 OK` - LLM response.