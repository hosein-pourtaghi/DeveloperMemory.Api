# Developer Memory API

The Developer Memory API is a .NET 10.0 Web API designed as a knowledge management and AI assistant gateway.

## Documentation
- [Project Summary](PROJECT_SUMMARY.md)
- [API Specification](API_SPECIFICATION.md)
- [Data Models](DATA_MODELS.md)
- [Configuration](CONFIGURATION.md)
- [Error Handling](ERROR_HANDLING.md)

## Overview
The system provides three core functionalities:
1. **Knowledge Management**: Store, search, and manage technical documents (Markdown + YAML frontmatter).
2. **Developer Profiles**: Store and manage developer profiles.
3. **AI Assistant Gateway**: Proxy requests to external LLM APIs with contextual information.

## Quick Start
1. **Restore Dependencies**:
   ```bash
   dotnet restore
   ```
2. **Run the Application**:
   ```bash
   dotnet run
   ```
3. **Access API**:
   - The API will be available at `https://localhost:7277`.
   - Swagger UI is available at `/swagger` for interactive testing.

## Configuration
Configure the application via `appsettings.json`:
- `FreeLlmApi`: LLM API connection settings.
- `Paths`: Directories for knowledge and profile storage.

## Technology Stack
- **Framework**: .NET 10.0
- **Logging**: Serilog
- **Documentation**: Swashbuckle (OpenAPI)
- **Data Format**: Markdown with YAML frontmatter