namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Deterministic statistical analysis for experiment variants.
/// Provides basic statistical comparison without external dependencies.
/// </summary>
public interface IExperimentStatisticsAnalyzer
{
    /// <summary>Compares quality scores between two variants.</summary>
    VariantComparisonResult CompareVariants(
        IReadOnlyList<double> variantAScores,
        IReadOnlyList<double> variantBScores,
        double significanceLevel = 0.05);
}

/// <summary>
/// Result of comparing two variant populations.
/// </summary>
public class VariantComparisonResult
{
    /// <summary>Sample count for variant A.</summary>
    public int SampleCountA { get; set; }

    /// <summary>Sample count for variant B.</summary>
    public int SampleCountB { get; set; }

    /// <summary>Mean score for variant A.</summary>
    public double MeanA { get; set; }

    /// <summary>Mean score for variant B.</summary>
    public double MeanB { get; set; }

    /// <summary>Variance for variant A.</summary>
    public double VarianceA { get; set; }

    /// <summary>Variance for variant B.</summary>
    public double VarianceB { get; set; }

    /// <summary>Standard deviation for variant A.</summary>
    public double StandardDeviationA { get; set; }

    /// <summary>Standard deviation for variant B.</summary>
    public double StandardDeviationB { get; set; }

    /// <summary>Difference between means (A - B).</summary>
    public double MeanDifference { get; set; }

    /// <summary>Whether the difference is statistically significant.</summary>
    public StatisticalSignificance Significance { get; set; }

    /// <summary>Computed p-value (approximation using Welch's t-test).</summary>
    public double? PValue { get; set; }

    /// <summary>Confidence interval for the mean difference (lower bound).</summary>
    public double ConfidenceIntervalLower { get; set; }

    /// <summary>Confidence interval for the mean difference (upper bound).</summary>
    public double ConfidenceIntervalUpper { get; set; }

    /// <summary>Summary of the analysis.</summary>
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Classification of statistical significance.
/// </summary>
public enum StatisticalSignificance
{
    /// <summary>Sufficient data and significant difference detected.</summary>
    Significant,

    /// <summary>Sufficient data but difference is not significant.</summary>
    NotSignificant,

    /// <summary>Insufficient data for meaningful analysis.</summary>
    InsufficientData
}
