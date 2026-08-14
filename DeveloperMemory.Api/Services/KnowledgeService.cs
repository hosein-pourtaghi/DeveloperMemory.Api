using DeveloperMemory.Api.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DeveloperMemory.Api.Services;

public class KnowledgeService
{
    private readonly string _knowledgeFolderPath;
    private List<KnowledgeDocument> _documents = new();

    public KnowledgeService(IConfiguration configuration)
    {
        _knowledgeFolderPath = configuration.GetValue<string>("AppSettings:Paths:KnowledgeFolder") ?? "./Knowledge";
    }

    public async Task<List<KnowledgeDocument>> LoadDocumentsAsync()
    {
        var documents = new List<KnowledgeDocument>();

        if (!Directory.Exists(_knowledgeFolderPath))
        {
            Directory.CreateDirectory(_knowledgeFolderPath);
            return documents;
        }

        var markdownFiles = Directory.GetFiles(_knowledgeFolderPath, "*.md");

        foreach (var filePath in markdownFiles)
        {
            var document = await ParseDocumentFromFileAsync(filePath);
            if (document != null)
            {
                documents.Add(document);
            }
        }

        _documents = documents;
        return documents;
    }

    public async Task<List<KnowledgeDocument>> ReindexDocumentsAsync()
    {
        return await LoadDocumentsAsync();
    }

    public List<SearchResult> SearchDocuments(string query, string? project = null, List<string>? tags = null)
    {
        var results = new List<SearchResult>();

        foreach (var document in _documents)
        {
            if (!string.IsNullOrEmpty(project) && document.Project != project)
                continue;

            if (tags != null && tags.Any() && !tags.Any(tag => document.Tags.Contains(tag)))
                continue;

            var score = CalculateRelevanceScore(query, document);
            if (score > 0)
            {
                results.Add(new SearchResult
                {
                    Id = document.Id,
                    Title = document.Title,
                    Content = document.Content,
                    Project = document.Project,
                    Tags = document.Tags,
                    Score = score,
                    FilePath = document.FilePath
                });
            }
        }

        return results.OrderByDescending(r => r.Score).ToList();
    }

    private async Task<KnowledgeDocument?> ParseDocumentFromFileAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);

        var frontmatterRegex = new Regex(@"---\r?\n(.*?)\r?\n---\r?\n(.*)", RegexOptions.Singleline);
        var match = frontmatterRegex.Match(content);

        var document = new KnowledgeDocument
        {
            FilePath = filePath,
            LastModified = File.GetLastWriteTimeUtc(filePath)
        };

        if (match.Success)
        {
            var metadata = match.Groups[1].Value;
            document.Content = match.Groups[2].Value;

            using var reader = new StringReader(metadata);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var keyValue = line.Split(':');
                if (keyValue.Length == 2)
                {
                    var key = keyValue[0].Trim().ToLowerInvariant();
                    var value = keyValue[1].Trim();

                    switch (key)
                    {
                        case "title":
                            document.Title = value;
                            break;
                        case "project":
                            document.Project = value;
                            break;
                        case "tags":
                            document.Tags.AddRange(value.Split(',').Select(s => s.Trim()));
                            break;
                    }
                }
            }
        }
        else
        {
            document.Title = Path.GetFileNameWithoutExtension(filePath);
            document.Content = content;
        }

        return document;
    }

    private double CalculateRelevanceScore(string query, KnowledgeDocument document)
    {
        var score = 0.0;
        var queryLower = query.ToLowerInvariant();

        if (document.Title.ToLowerInvariant().Contains(queryLower))
            score += 0.5;

        if (document.Content.ToLowerInvariant().Contains(queryLower))
            score += 0.3;

        if (document.Project.ToLowerInvariant().Contains(queryLower))
            score += 0.1;

        foreach (var tag in document.Tags)
        {
            if (tag.ToLowerInvariant().Contains(queryLower))
                score += 0.1;
        }

        return score;
    }
}