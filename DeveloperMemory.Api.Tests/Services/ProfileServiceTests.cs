using DeveloperMemory.Api.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeveloperMemory.Api.Tests.Services;

public class ProfileServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProfileService _service;

    public ProfileServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dm_profile_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AppSettings:Paths:ProfilesFolder", _tempDir }
            })
            .Build();

        _service = new ProfileService(config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    private async Task WriteProfileFile(string fileName, string content)
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), content);
    }

    [Fact]
    public async Task LoadProfilesAsync_EmptyDirectory_ReturnsEmptyList()
    {
        var profiles = await _service.LoadProfilesAsync();
        Assert.Empty(profiles);
    }

    [Fact]
    public async Task LoadProfilesAsync_WithFrontmatter_ParsesCorrectly()
    {
        await WriteProfileFile("dev.md", @"---
name: John Doe
role: Backend Developer
skills: C#, ASP.NET, TypeScript
experience: 5 years
---

John is a full-stack developer specializing in .NET.");

        var profiles = await _service.LoadProfilesAsync();
        Assert.Single(profiles);
        Assert.Equal("John Doe", profiles[0].Name);
        Assert.Equal("Backend Developer", profiles[0].Role);
        Assert.Contains("C#", profiles[0].Skills);
        Assert.Contains("ASP.NET", profiles[0].Skills);
        Assert.Contains("TypeScript", profiles[0].Skills);
        Assert.Equal("5 years", profiles[0].Experience);
        Assert.Contains("full-stack developer", profiles[0].Bio);
    }

    [Fact]
    public async Task LoadProfilesAsync_NoFrontmatter_ReturnsNull()
    {
        await WriteProfileFile("no-frontmatter.md", "Just some text without frontmatter.");

        var profiles = await _service.LoadProfilesAsync();
        Assert.Empty(profiles);
    }

    [Fact]
    public async Task LoadProfilesAsync_MultipleProfiles_LoadsAll()
    {
        await WriteProfileFile("dev1.md", @"---
name: Alice
role: Frontend
---

Alice's bio.");
        await WriteProfileFile("dev2.md", @"---
name: Bob
role: Backend
---

Bob's bio.");

        var profiles = await _service.LoadProfilesAsync();
        Assert.Equal(2, profiles.Count);
    }

    [Fact]
    public async Task LoadProfilesAsync_AssignsStableIds()
    {
        await WriteProfileFile("dev.md", @"---
name: Test Dev
role: Dev
---

Bio.");

        var profiles1 = await _service.LoadProfilesAsync();
        var profiles2 = await _service.LoadProfilesAsync();

        Assert.Equal(profiles1[0].Id, profiles2[0].Id);
    }

    [Fact]
    public async Task LoadProfilesAsync_MissingOptionalFields_DefaultsEmpty()
    {
        await WriteProfileFile("minimal.md", @"---
name: Minimal
---

Bio only.");

        var profiles = await _service.LoadProfilesAsync();
        Assert.Single(profiles);
        Assert.Equal("Minimal", profiles[0].Name);
        Assert.Equal(string.Empty, profiles[0].Role);
        Assert.Empty(profiles[0].Skills);
        Assert.Equal(string.Empty, profiles[0].Experience);
    }

    [Fact]
    public async Task LoadProfilesAsync_SetsFilePath()
    {
        await WriteProfileFile("dev.md", @"---
name: Dev
role: Role
---

Bio.");

        var profiles = await _service.LoadProfilesAsync();
        Assert.Contains("dev.md", profiles[0].FilePath);
    }

    [Fact]
    public async Task LoadProfilesAsync_SetsLastModified()
    {
        await WriteProfileFile("dev.md", @"---
name: Dev
role: Role
---

Bio.");

        var profiles = await _service.LoadProfilesAsync();
        Assert.True(profiles[0].LastModified > DateTime.UtcNow.AddMinutes(-1));
    }
}
