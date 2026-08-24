using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeveloperMemory.Api.Tests.Services;

public class KnowledgeServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly KnowledgeService _service;

    public KnowledgeServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dm_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AppSettings:Paths:KnowledgeFolder", _tempDir }
            })
            .Build();

        _service = new KnowledgeService(config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    private async Task WriteKnowledgeFile(string fileName, string content)
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), content);
    }

    [Fact]
    public async Task LoadDocumentsAsync_EmptyDirectory_ReturnsEmptyList()
    {
        var docs = await _service.LoadDocumentsAsync();
        Assert.Empty(docs);
    }

    [Fact]
    public async Task LoadDocumentsAsync_WithFrontmatter_ParsesCorrectly()
    {
        await WriteKnowledgeFile("test.md", @"---
title: Coding Standards
project: MyProject
tags: csharp, dotnet
---

# Coding Standards

Use PascalCase for public members.");

        var docs = await _service.LoadDocumentsAsync();
        Assert.Single(docs);
        Assert.Equal("Coding Standards", docs[0].Title);
        Assert.Equal("MyProject", docs[0].Project);
        Assert.Contains("csharp", docs[0].Tags);
        Assert.Contains("dotnet", docs[0].Tags);
        Assert.Contains("Coding Standards", docs[0].Content);
    }

    [Fact]
    public async Task LoadDocumentsAsync_NameAlias_ParsesTitle()
    {
        // Knowledge docs use 'name:' instead of 'title:'
        await WriteKnowledgeFile("test.md", @"---
name: AI Agent Rules
project: DevMemory
tags: agents
---

Agent behavior rules here.");

        var docs = await _service.LoadDocumentsAsync();
        Assert.Single(docs);
        Assert.Equal("AI Agent Rules", docs[0].Title);
    }

    [Fact]
    public async Task LoadDocumentsAsync_NoFrontmatter_UsesFileName()
    {
        await WriteKnowledgeFile("my-doc.md", "Just plain content without frontmatter.");

        var docs = await _service.LoadDocumentsAsync();
        Assert.Single(docs);
        Assert.Equal("my-doc", docs[0].Title);
        Assert.Contains("plain content", docs[0].Content);
    }

    [Fact]
    public async Task LoadDocumentsAsync_MultipleFiles_LoadsAll()
    {
        await WriteKnowledgeFile("doc1.md", @"---
title: First Doc
---

Content 1.");
        await WriteKnowledgeFile("doc2.md", @"---
title: Second Doc
---

Content 2.");

        var docs = await _service.LoadDocumentsAsync();
        Assert.Equal(2, docs.Count);
    }

    [Fact]
    public async Task LoadDocumentsAsync_AssignsStableIds()
    {
        await WriteKnowledgeFile("test.md", @"---
title: Test
---

Content.");

        var docs1 = await _service.LoadDocumentsAsync();
        var docs2 = await _service.LoadDocumentsAsync();

        Assert.Equal(docs1[0].Id, docs2[0].Id);
    }

    [Fact]
    public async Task LoadDocumentsAsync_MissingOptionalFields_DefaultsEmpty()
    {
        await WriteKnowledgeFile("minimal.md", @"---
title: Minimal Doc
---

Content.");

        var docs = await _service.LoadDocumentsAsync();
        Assert.Single(docs);
        Assert.Equal(string.Empty, docs[0].Project);
        Assert.Empty(docs[0].Tags);
    }

    [Fact]
    public async Task SearchDocuments_NoQuery_ReturnsEmpty()
    {
        await WriteKnowledgeFile("test.md", @"---
title: Test
---

Some content.");

        await _service.LoadDocumentsAsync();
        var results = _service.SearchDocuments("");
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchDocuments_MatchingTitle_ReturnsResult()
    {
        await WriteKnowledgeFile("test.md", @"---
title: CSharp Standards
---

Content about C#.");
        await _service.LoadDocumentsAsync();

        var results = _service.SearchDocuments("CSharp");
        Assert.Single(results);
        Assert.Equal("CSharp Standards", results[0].Title);
    }

    [Fact]
    public async Task SearchDocuments_MatchingContent_ReturnsResult()
    {
        await WriteKnowledgeFile("test.md", @"---
title: Standards
---

Important coding guidelines.");
        await _service.LoadDocumentsAsync();

        var results = _service.SearchDocuments("coding");
        Assert.Single(results);
    }

    [Fact]
    public async Task SearchDocuments_NoMatch_ReturnsEmpty()
    {
        await WriteKnowledgeFile("test.md", @"---
title: Standards
---

Content about coding.");
        await _service.LoadDocumentsAsync();

        var results = _service.SearchDocuments("xyz");
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchDocuments_ByProject_FiltersCorrectly()
    {
        await WriteKnowledgeFile("doc1.md", @"---
title: Doc 1
project: ProjectA
---
Content 1.");
        await WriteKnowledgeFile("doc2.md", @"---
title: Doc 2
project: ProjectB
---
Content 2.");
        await _service.LoadDocumentsAsync();

        var results = _service.SearchDocuments("Doc", project: "ProjectA");
        Assert.Single(results);
        Assert.Equal("Doc 1", results[0].Title);
    }

    [Fact]
    public async Task SearchDocuments_OrderByScore_HighestFirst()
    {
        await WriteKnowledgeFile("doc1.md", @"---
title: AI Rules
tags: ai, rules
---
AI content.");
        await WriteKnowledgeFile("doc2.md", @"---
title: AI Agent Standards
tags: ai
---
General content.");
        await _service.LoadDocumentsAsync();

        var results = _service.SearchDocuments("AI");
        Assert.Equal(2, results.Count);
        // Title match should score higher than content-only match
        Assert.True(results[0].Score >= results[1].Score);
    }

    [Fact]
    public async Task CreateDocumentAsync_CreatesFileOnDisk()
    {
        await _service.LoadDocumentsAsync();
        var doc = await _service.CreateDocumentAsync(
            "New Document",
            "Content here.",
            project: "TestProject",
            tags: new List<string> { "tag1", "tag2" });

        Assert.Equal("New Document", doc.Title);
        Assert.Equal("TestProject", doc.Project);
        Assert.Contains("tag1", doc.Tags);

        // Verify file exists on disk
        var files = Directory.GetFiles(_tempDir, "*.md");
        Assert.Single(files);
    }

    [Fact]
    public async Task ReindexDocumentsAsync_ReloadsFromDisk()
    {
        await _service.LoadDocumentsAsync();
        Assert.Empty(await _service.LoadDocumentsAsync());

        // Add a file after initial load
        await WriteKnowledgeFile("new.md", @"---
title: New Doc
---

Content.");

        var docs = await _service.ReindexDocumentsAsync();
        Assert.Single(docs);
    }

    [Fact]
    public async Task LoadDocumentsAsync_ValueWithColon_PreservesFullValue()
    {
        // Verify the colon fix: values containing ':' should not be truncated
        await WriteKnowledgeFile("test.md", @"---
title: How to: Configure Serilog
project: MyApp
---

Content.");

        var docs = await _service.LoadDocumentsAsync();
        Assert.Single(docs);
        Assert.Equal("How to: Configure Serilog", docs[0].Title);
    }

    [Fact]
    public async Task LoadDocumentsAsync_ProjectWithColon_PreservesFullValue()
    {
        await WriteKnowledgeFile("test.md", @"---
title: Test
project: My:Project
---

Content.");

        var docs = await _service.LoadDocumentsAsync();
        Assert.Single(docs);
        Assert.Equal("My:Project", docs[0].Project);
    }
}
