namespace DeveloperMemory.Api.Infrastructure.Authentication;

public class ApiKeyUser
{
    public string UserId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public IReadOnlyList<string> Scopes { get; set; } = [];
}
