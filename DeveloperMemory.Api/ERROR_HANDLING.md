# Error Handling and Troubleshooting

This document outlines common error scenarios and how to troubleshoot them in the Developer Memory API.

## Common HTTP Status Codes

| Status Code | Meaning | Common Cause |
| :--- | :--- | :--- |
| `200 OK` | Success | Request processed successfully. |
| `400 Bad Request` | Invalid Input | Missing required fields in `PromptRequest` or invalid file path. |
| `404 Not Found` | Resource Missing | Document or profile ID does not exist. |
| `500 Internal Server Error` | Server Error | Issues connecting to the LLM API or file system access errors. |

## Troubleshooting

### 1. LLM API Connection Issues
- **Symptom**: `500 Internal Server Error` when calling `/api/Proxy`.
- **Check**:
  - Verify `FreeLlmApi:BaseUrl` in `appsettings.json` is correct and reachable.
  - Ensure the LLM service is running.
  - Check if `FreeLlmApi:ApiKey` is configured if required by your provider.

### 2. Document/Profile Loading Errors
- **Symptom**: Documents or profiles are not appearing in search results.
- **Check**:
  - Verify `Paths:KnowledgeFolder` and `Paths:ProfilesFolder` point to valid directories.
  - Ensure files are in Markdown format with valid YAML frontmatter.
  - Trigger a reindex using `POST /api/Knowledge/reindex`.

### 3. Logging
- **Check**:
  - Logs are stored in the `logs/` directory.
  - Check `devmemory-.log` for detailed error messages and stack traces.