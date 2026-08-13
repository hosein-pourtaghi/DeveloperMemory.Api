using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeveloperMemory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KnowledgeController : ControllerBase
{
    private readonly KnowledgeService _knowledgeService;

    public KnowledgeController(KnowledgeService knowledgeService)
    {
        _knowledgeService = knowledgeService;
    }

    [HttpGet]
    public ActionResult<List<SearchResult>> SearchDocuments([FromQuery] string query, [FromQuery] string? project, [FromQuery] List<string>? tags)
    {
        var results = _knowledgeService.SearchDocuments(query, project, tags);
        return Ok(results);
    }

    [HttpGet("documents")]
    public async Task<ActionResult<List<KnowledgeDocument>>> GetDocuments()
    {
        var documents = await _knowledgeService.LoadDocumentsAsync();
        return Ok(documents);
    }

    [HttpPost("reindex")]
    public async Task<ActionResult<List<KnowledgeDocument>>> Reindex()
    {
        var documents = await _knowledgeService.ReindexDocumentsAsync();
        return Ok(documents);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<KnowledgeDocument>> GetDocument(Guid id)
    {
        var documents = await _knowledgeService.LoadDocumentsAsync();
        var document = documents.FirstOrDefault(d => d.Id == id);
        
        if (document == null)
        {
            return NotFound();
        }
        
        return Ok(document);
    }
}