using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Configuration;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class PromptProfileTests
{

    [Fact]
    public void PromptProfile_SetConfiguration_UpdatesJson()
    {
        var profile = new PromptProfile();
        var config = new PromptProfileConfiguration { TokenBudget = 6000 };

        profile.SetConfiguration(config);

        var parsed = profile.GetConfiguration();
        Assert.Equal(6000, parsed.TokenBudget);
    }

    [Fact]
    public void PromptProfileProvider_DefaultProfilesExist()
    {
        var provider = new PromptProfileProvider(
            new Mock<ILogger<PromptProfileProvider>>().Object);

        var profiles = provider.GetEnabledProfilesAsync().Result;

        Assert.NotEmpty(profiles);
        Assert.Contains(profiles, p => p.Name == "DefaultDeveloper");
    }

    [Fact]
    public void PromptProfileProvider_CreateProfile_ReturnsNewId()
    {
        var provider = new PromptProfileProvider(
            new Mock<ILogger<PromptProfileProvider>>().Object);

        var profile = provider.CreateAsync(new PromptProfile
        {
            Name = "TestProfile",
            Description = "Test"
        }).Result;

        Assert.NotEqual(Guid.Empty, profile.Id);
        Assert.Equal("TestProfile", profile.Name);
    }
}
