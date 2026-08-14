using DeveloperMemory.Api.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DeveloperMemory.Api.Services;

public class ProfileService
{
    private readonly string _profilesFolderPath;

    public ProfileService(IConfiguration configuration)
    {
        _profilesFolderPath = configuration.GetValue<string>("AppSettings:Paths:ProfilesFolder") ?? "./Profiles";
    }

    public async Task<List<DeveloperProfile>> LoadProfilesAsync()
    {
        var profiles = new List<DeveloperProfile>();

        if (!Directory.Exists(_profilesFolderPath))
        {
            Directory.CreateDirectory(_profilesFolderPath);
            return profiles;
        }

        var markdownFiles = Directory.GetFiles(_profilesFolderPath, "*.md");

        foreach (var filePath in markdownFiles)
        {
            var profile = await ParseProfileFromFileAsync(filePath);
            if (profile != null)
            {
                profiles.Add(profile);
            }
        }

        return profiles;
    }

    public async Task<DeveloperProfile?> ParseProfileFromFileAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);

        var frontmatterRegex = new Regex(@"---\r?\n(.*?)\r?\n---\r?\n(.*)", RegexOptions.Singleline);
        var match = frontmatterRegex.Match(content);

        if (!match.Success)
        {
            return null;
        }

        var metadata = match.Groups[1].Value;
        var bio = match.Groups[2].Value;

        var profile = new DeveloperProfile
        {
            FilePath = filePath,
            LastModified = File.GetLastWriteTimeUtc(filePath),
            Bio = bio
        };

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
                    case "name":
                        profile.Name = value;
                        break;
                    case "role":
                        profile.Role = value;
                        break;
                    case "skills":
                        profile.Skills.AddRange(value.Split(',').Select(s => s.Trim()));
                        break;
                    case "experience":
                        profile.Experience = value;
                        break;
                }
            }
        }

        return profile;
    }
}