# API Documentation

## Base URL
`https://localhost:7277`

## Knowledge Controller (`/api/Knowledge`)

### Search Documents
- **Method**: GET
- **Path**: `/api/Knowledge`
- **Description**: Search documents by query, project, and tags.
- **Parameters**:
  - `query` (string, optional): Search term.
  - `project` (string, optional): Filter by project.
  - `tags` (string, optional): Comma-separated list of tags.
- **Response**: `200 OK` - List of `SearchResult` objects.

### Get All Documents
- **Method**: GET
- **Path**: `/api/Knowledge/documents`
- **Description**: Retrieve all stored documents.
- **Response**: `200 OK` - List of `KnowledgeDocument` objects.

### Reindex Documents
- **Method**: POST
- **Path**: `/api/Knowledge/reindex`
- **Description**: Trigger a reindexing of all documents.
- **Response**: `200 OK` - Success message.

### Get Document by ID
- **Method**: GET
- **Path**: `/api/Knowledge/{id}`
- **Description**: Retrieve a specific document by ID.
- **Response**: `200 OK` - `KnowledgeDocument` object.

## Profiles Controller (`/api/Profiles`)

### Get All Profiles
- **Method**: GET
- **Path**: `/api/Profiles`
- **Description**: Retrieve all developer profiles.
- **Response**: `200 OK` - List of `DeveloperProfile` objects.

### Load Profile
- **Method**: POST
- **Path**: `/api/Profiles`
- **Description**: Load a profile from a file path.
- **Request Body**: `text/plain` (file path).
- **Response**: `200 OK` - Success message.

## Proxy Controller (`/api/Proxy`)

### AI Assistant Query
- **Method**: POST
- **Path**: `/api/Proxy`
- **Description**: Forward a query to the LLM API with context.
- **Request Body**: `PromptRequest` (JSON).
- **Response**: `200 OK` - LLM response.