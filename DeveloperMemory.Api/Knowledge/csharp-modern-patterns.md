---
title: Modern C# Patterns
project: DeveloperMemory
tags: csharp, dotnet, patterns, best-practices
---

# Modern C# Patterns

## File-Scoped Namespaces
```csharp
namespace DeveloperMemory.Api.Models; // Instead of block-scoped
```

## Primary Constructors (C# 12+)
```csharp
public class KnowledgeService(IConfiguration configuration)
{
    private readonly string _path = configuration.GetValue<string>("AppSettings:Paths:KnowledgeFolder") ?? "./Knowledge";
}
```

## Record Types
```csharp
public record SearchResult(Guid Id, string Title, double Score);
```

## Pattern Matching
```csharp
var result = document switch
{
    { Tags.Count: > 0 } => "Has tags",
    { Title.Length: > 10 } => "Long title",
    _ => "Default"
};
```

## Null Safety
```csharp
var title = document?.Title ?? "Untitled";
var tags = document?.Tags?.FirstOrDefault() ?? "no-tags";
```

## Async Best Practices
- Use `ValueTask` for hot paths
- Configure await: `await query.ConfigureAwait(false)`
- Use `IAsyncEnumerable` for streaming
- CancellationToken propagation through async chains

## Collection Expressions (C# 12)
```csharp
List<string> tags = ["dotnet", "api", "best-practices"];
int[] numbers = [1, 2, 3, 4, 5];
```
