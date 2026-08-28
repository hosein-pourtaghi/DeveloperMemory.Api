using DeveloperMemory.Domain.Entities;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class PromptProfileVersionTests
{
    [Fact]
    public void GetConfiguration_InvalidJson_ReturnsDefault()
    {
        var version = new PromptProfileVersion
        {
            ConfigurationJson = "{invalid json"
        };

        var config = version.GetConfiguration();

        Assert.NotNull(config);
    }

    [Fact]
    public void PromptAuditEvent_AllEventTypes_Exist()
    {
        var types = Enum.GetValues<PromptAuditEventType>();

        Assert.Contains(PromptAuditEventType.PromptAnalyzed, types);
        Assert.Contains(PromptAuditEventType.IntentResolved, types);
        Assert.Contains(PromptAuditEventType.MemoryContextSelected, types);
        Assert.Contains(PromptAuditEventType.ProfileSelected, types);
        Assert.Contains(PromptAuditEventType.ProfileVersionCreated, types);
        Assert.Contains(PromptAuditEventType.ProfileRollback, types);
        Assert.Contains(PromptAuditEventType.PromptOptimized, types);
        Assert.Contains(PromptAuditEventType.OptimizationRejected, types);
        Assert.Contains(PromptAuditEventType.FallbackActivated, types);
        Assert.Contains(PromptAuditEventType.PromptValidationFailed, types);
        Assert.Contains(PromptAuditEventType.QualityGateFailed, types);
        Assert.Contains(PromptAuditEventType.QualityGatePassed, types);
        Assert.Contains(PromptAuditEventType.ProcessingRecordCreated, types);
    }
}
