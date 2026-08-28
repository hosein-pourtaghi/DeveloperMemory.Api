using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Configuration;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class InMemoryPromptMetricsTests
{
    private readonly InMemoryPromptMetrics _metrics = new();

    [Fact]
    public void RecordProcessingRequest_IncrementsCount()
    {
        _metrics.RecordProcessingRequest(new PromptProcessingMetric
        {
            Intent = "Coding",
            ProcessingDurationMs = 100
        });

        var summary = _metrics.GetSummary();

        Assert.Equal(1, summary.TotalRequests);
    }

    [Fact]
    public void GetSummary_EmptyMetrics_ReturnsZeros()
    {
        var summary = _metrics.GetSummary();

        Assert.Equal(0, summary.TotalRequests);
        Assert.Equal(0, summary.AverageQualityScore);
    }

    [Fact]
    public void GetSummary_WithDateFilter_FiltersCorrectly()
    {
        _metrics.RecordProcessingRequest(new PromptProcessingMetric
        {
            Intent = "Old",
            Timestamp = DateTime.UtcNow.AddDays(-10)
        });
        _metrics.RecordProcessingRequest(new PromptProcessingMetric
        {
            Intent = "New",
            Timestamp = DateTime.UtcNow
        });

        var summary = _metrics.GetSummary(from: DateTime.UtcNow.AddDays(-1));

        Assert.Equal(1, summary.TotalRequests);
    }

    [Fact]
    public void RecordExperimentResult_TracksByVariant()
    {
        var experimentId = Guid.NewGuid();
        var variantId = Guid.NewGuid();

        _metrics.RecordExperimentResult(new ExperimentResultMetric
        {
            ExperimentId = experimentId,
            VariantId = variantId,
            VariantName = "control",
            QualityScore = 0.80,
            QualityGatePassed = true
        });

        var metrics = _metrics.GetExperimentMetrics(experimentId);

        Assert.Equal(1, metrics.TotalRequests);
        Assert.Single(metrics.Variants);
        Assert.Equal(0.80, metrics.Variants[0].AverageQualityScore, 2);
    }

    [Fact]
    public void GetSummary_CalculatesRates()
    {
        _metrics.RecordProcessingRequest(new PromptProcessingMetric { QualityGatePassed = true, WasFallbackUsed = false });
        _metrics.RecordProcessingRequest(new PromptProcessingMetric { QualityGatePassed = true, WasFallbackUsed = false });
        _metrics.RecordProcessingRequest(new PromptProcessingMetric { QualityGatePassed = false, WasFallbackUsed = true });

        var summary = _metrics.GetSummary();

        Assert.Equal(3, summary.TotalRequests);
        Assert.Equal(2, summary.SuccessfulRequests);
        Assert.Equal(1, summary.FailedRequests);
        Assert.Equal(1, summary.FallbackCount);
    }
}
