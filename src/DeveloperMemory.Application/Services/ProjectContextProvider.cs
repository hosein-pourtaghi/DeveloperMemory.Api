using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Project context provider using existing project infrastructure.
/// Integrates with project metadata and knowledge documents.
/// </summary>
public class ProjectContextProvider : IProjectContextProvider
{
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectContextProvider> _logger;

    public ProjectContextProvider(
        IProjectService projectService,
        ILogger<ProjectContextProvider> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    public bool IsAvailable => true;

    public async Task<ProjectContext?> GetContextAsync(
        Guid? projectId = null,
        string? workspaceId = null,
        CancellationToken ct = default)
    {
        if (!projectId.HasValue)
        {
            return null;
        }

        try
        {
            var project = await _projectService.GetByIdAsync(projectId.Value, ct);
            if (project == null)
            {
                return null;
            }

            var context = new ProjectContext
            {
                ProjectId = projectId,
                WorkspaceId = workspaceId,
                ProjectName = project.Name,
                ArchitectureRules = ExtractArchitectureRules(project),
                TechnologyStack = ExtractTechnologyStack(project),
                CodingConventions = ExtractCodingConventions(project),
                ArchitecturalDecisions = ExtractDecisions(project),
                EstimatedTokens = 200 // Approximate base overhead
            };

            _logger.LogDebug(
                "Project context loaded: {Name}, {Rules} rules, {Stack} technologies",
                context.ProjectName, context.ArchitectureRules.Count, context.TechnologyStack.Count);

            return context;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load project context for {ProjectId}", projectId);
            return null;
        }
    }

    private static List<string> ExtractArchitectureRules(ProjectDto project)
    {
        var rules = new List<string>();

        // Extract from project configuration if available
        if (!string.IsNullOrEmpty(project.ConfigurationJson))
        {
            try
            {
                var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                    project.ConfigurationJson);

                if (config != null)
                {
                    if (config.TryGetValue("architecture", out var arch) && arch is System.Text.Json.JsonElement archElement)
                    {
                        if (archElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var item in archElement.EnumerateArray())
                            {
                                rules.Add(item.GetString() ?? string.Empty);
                            }
                        }
                    }

                    if (config.TryGetValue("rules", out var rulesElement) && rulesElement is System.Text.Json.JsonElement rulesJson)
                    {
                        if (rulesJson.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var item in rulesJson.EnumerateArray())
                            {
                                rules.Add(item.GetString() ?? string.Empty);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Configuration parsing failed — continue with empty rules
            }
        }

        return rules.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
    }

    private static List<string> ExtractTechnologyStack(ProjectDto project)
    {
        var stack = new List<string>();

        if (!string.IsNullOrEmpty(project.ConfigurationJson))
        {
            try
            {
                var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                    project.ConfigurationJson);

                if (config != null && config.TryGetValue("stack", out var stackElement) &&
                    stackElement is System.Text.Json.JsonElement stackJson)
                {
                    if (stackJson.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var item in stackJson.EnumerateArray())
                        {
                            stack.Add(item.GetString() ?? string.Empty);
                        }
                    }
                }
            }
            catch
            {
                // Continue with empty stack
            }
        }

        return stack.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    private static List<string> ExtractCodingConventions(ProjectDto project)
    {
        var conventions = new List<string>();

        if (!string.IsNullOrEmpty(project.ConfigurationJson))
        {
            try
            {
                var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                    project.ConfigurationJson);

                if (config != null && config.TryGetValue("conventions", out var convElement) &&
                    convElement is System.Text.Json.JsonElement convJson)
                {
                    if (convJson.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var item in convJson.EnumerateArray())
                        {
                            conventions.Add(item.GetString() ?? string.Empty);
                        }
                    }
                }
            }
            catch
            {
                // Continue with empty conventions
            }
        }

        return conventions.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
    }

    private static List<string> ExtractDecisions(ProjectDto project)
    {
        var decisions = new List<string>();

        if (!string.IsNullOrEmpty(project.ConfigurationJson))
        {
            try
            {
                var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                    project.ConfigurationJson);

                if (config != null && config.TryGetValue("decisions", out var decElement) &&
                    decElement is System.Text.Json.JsonElement decJson)
                {
                    if (decJson.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var item in decJson.EnumerateArray())
                        {
                            decisions.Add(item.GetString() ?? string.Empty);
                        }
                    }
                }
            }
            catch
            {
                // Continue with empty decisions
            }
        }

        return decisions.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
    }
}
