using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using DeveloperMemory.Api.Infrastructure.Configuration;

namespace DeveloperMemory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfilesController : ControllerBase
{
    private readonly ProfileService _profileService;
    private readonly PathSettings _paths;

    public ProfilesController(ProfileService profileService, IOptions<PathSettings> paths)
    {
        _profileService = profileService;
        _paths = paths.Value;
    }

    [HttpGet]
    public async Task<ActionResult<List<DeveloperProfile>>> GetProfiles()
    {
        var profiles = await _profileService.LoadProfilesAsync();
        return Ok(profiles);
    }

    [HttpPost]
    public async Task<ActionResult<DeveloperProfile>> LoadProfile([FromBody] string filePath)
    {
        // Security: validate the path is within the configured profiles directory
        var resolvedPath = Path.GetFullPath(filePath);
        var profilesDir = Path.GetFullPath(_paths.ProfilesFolder);
        if (!resolvedPath.StartsWith(profilesDir, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("File path must be within the configured profiles directory");
        }

        var profile = await _profileService.ParseProfileFromFileAsync(filePath);
        if (profile == null)
        {
            return BadRequest("Invalid profile file or format");
        }
        
        return Ok(profile);
    }
}