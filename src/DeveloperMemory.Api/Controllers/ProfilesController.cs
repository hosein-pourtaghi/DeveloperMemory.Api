using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeveloperMemory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfilesController : ControllerBase
{
    private readonly ProfileService _profileService;

    public ProfilesController(ProfileService profileService)
    {
        _profileService = profileService;
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
        var profile = await _profileService.ParseProfileFromFileAsync(filePath);
        if (profile == null)
        {
            return BadRequest("Invalid profile file or format");
        }
        
        return Ok(profile);
    }
}