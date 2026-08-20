using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DeveloperMemory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProxyController : ControllerBase
{
    private readonly FreeLlmApiClient _freeLlmApiClient;
    private readonly PromptBuilder _promptBuilder;
    private readonly KnowledgeService _knowledgeService;
    private readonly ProfileService _profileService;

    private const string DefaultSystemPrompt = @"You are an expert software engineer and coding assistant. You write clean, maintainable, and well-documented code following best practices for the developer's tech stack.

Coding Style:
- Use clear, descriptive variable and function names
- Follow SOLID principles
- Add XML comments to public methods
- Use appropriate design patterns
- Handle errors gracefully with proper logging
- Write code that is easy to test

When answering:
- Provide working code examples
- Explain your reasoning
- Consider edge cases and error handling
- Suggest improvements when appropriate
- Match the developer's existing code style";

    public ProxyController(
        FreeLlmApiClient freeLlmApiClient,
        PromptBuilder promptBuilder,
        KnowledgeService knowledgeService,
        ProfileService profileService)
    {
        _freeLlmApiClient = freeLlmApiClient;
        _promptBuilder = promptBuilder;
        _knowledgeService = knowledgeService;
        _profileService = profileService;
    }

    [HttpPost]
    public async Task<ActionResult<string>> ForwardRequest([FromBody] PromptRequest request)
    {
        try
        {
            // Load profiles and documents
            var profiles = await _profileService.LoadProfilesAsync();
            var documents = await _knowledgeService.LoadDocumentsAsync();

            // Search documents
            var searchResults = _knowledgeService.SearchDocuments(
                request.Query ?? string.Empty,
                request.Project,
                request.Tags);

            // Apply default system prompt if none provided
            if (string.IsNullOrEmpty(request.SystemPrompt))
            {
                request.SystemPrompt = DefaultSystemPrompt;
            }

            // Build prompt
            var prompt = _promptBuilder.BuildPrompt(
                request,
                profiles,
                searchResults);

            // Send to FreeLlm API with model override support
            var response = await _freeLlmApiClient.SendPromptAsync(prompt, request.Model);

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error processing request: {ex.Message}");
        }
    }
}