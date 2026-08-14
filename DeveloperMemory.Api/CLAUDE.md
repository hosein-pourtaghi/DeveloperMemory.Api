# CLAUDE.md - Coding Standards

## Project Overview
Developer Memory API is a .NET 10.0 Web API for knowledge management and AI assistant gateway.

## Code Style Guidelines

### Naming Conventions
- Use PascalCase for classes, methods, and properties
- Use camelCase for local variables and parameters
- Use `_camelCase` for private fields

### File Organization
- Controllers in `Controllers/` directory
- Services in `Services/` directory
- Models in `Models/` directory
- Configuration in `Infrastructure/Configuration/`

### Documentation
- Add XML comments to public methods
- Update relevant documentation files when adding features
- Follow existing documentation structure

### Error Handling
- Use try-catch blocks in controllers
- Return appropriate HTTP status codes
- Log errors using Serilog

### Testing
- Write unit tests for service methods
- Test edge cases and error conditions
- Maintain test coverage above 80%

## Git Workflow
- Create feature branches for new functionality
- Write descriptive commit messages
- Create pull requests for code review
- Squash commits before merging

## Dependencies
- Keep dependencies up to date
- Review security advisories regularly
- Document new dependencies in README