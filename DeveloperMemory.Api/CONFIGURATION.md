# Configuration Reference

The Developer Memory API uses `appsettings.json` for configuration.

## Configuration Schema

### `FreeLlmApi`
Settings for the external LLM API proxy.
- `BaseUrl` (string): The base URL of the LLM API (e.g., `http://localhost:3001/v1`).
- `ApiKey` (string): The API key for authentication with the LLM provider.

### `Paths`
File system paths for data storage.
- `KnowledgeFolder` (string): Path to the directory containing knowledge documents (Markdown).
- `ProfilesFolder` (string): Path to the directory containing developer profiles (Markdown).

### `Logging` & `Serilog`
Standard ASP.NET Core and Serilog logging configuration.
- `Serilog:WriteTo:File:Args:path`: Path to the log file (default: `logs/devmemory-.log`).
- `Serilog:WriteTo:File:Args:rollingInterval`: How often to roll the log file (default: `Day`).
- `Serilog:WriteTo:File:Args:retainedFileCountLimit`: Number of log files to keep (default: `30`).

## Environment Variables
You can override these settings using environment variables, for example:
- `FreeLlmApi__ApiKey`
- `Paths__KnowledgeFolder`

## Configuration Examples

### appsettings.json
```json
{
  "FreeLlmApi": {
    "BaseUrl": "http://localhost:3001/v1",
    "ApiKey": "your-api-key"
  },
  "Paths": {
    "KnowledgeFolder": "Knowledge",
    "ProfilesFolder": "Profiles"
  },
  "Logging": {
    "Serilog": {
      "WriteTo": {
        "File": {
          "Args": {
            "path": "logs/devmemory-.log",
            "rollingInterval": "Day",
            "retainedFileCountLimit": 30
          }
        }
      }
    }
  }
}
```

### launchSettings.json
```json
{
  "iisSettings": {
    "windowsAuthentication": false,
    "anonymousAuthentication": true,
    "iisExpress": {
      "applicationUrl": "https://localhost:7277",
      "sslPort": 7477
    }
  },
  "profiles": {
    "DeveloperMemory.Api": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:7277",
      "sslPort": 7477
    }
  }
}