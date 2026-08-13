using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
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

            // Build prompt
            var prompt = _promptBuilder.BuildPrompt(
                request,
                profiles,
                searchResults);

            // Send to FreeLlm API
            var response = await _freeLlmApiClient.SendPromptAsync(prompt);

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error processing request: {ex.Message}");
        }
    }
}