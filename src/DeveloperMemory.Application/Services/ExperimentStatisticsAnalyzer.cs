using DeveloperMemory.Application.Contracts;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Deterministic statistical analyzer for experiment variants.
/// Uses Welch's t-test approximation for comparing two populations.
/// No external statistics packages required.
/// </summary>
public class ExperimentStatisticsAnalyzer : IExperimentStatisticsAnalyzer
{
    /// <summary>
    /// Minimum sample count per variant for meaningful statistical analysis.
    /// Below this threshold, the result is InsufficientData.
    /// </summary>
    private const int MinSampleSize = 5;

    public VariantComparisonResult CompareVariants(
        IReadOnlyList<double> variantAScores,
        IReadOnlyList<double> variantBScores,
        double significanceLevel = 0.05)
    {
        var result = new VariantComparisonResult
        {
            SampleCountA = variantAScores.Count,
            SampleCountB = variantBScores.Count
        };

        // Insufficient data check
        if (variantAScores.Count < MinSampleSize || variantBScores.Count < MinSampleSize)
        {
            result.Significance = StatisticalSignificance.InsufficientData;
            result.MeanA = variantAScores.Count > 0 ? variantAScores.Average() : 0;
            result.MeanB = variantBScores.Count > 0 ? variantBScores.Average() : 0;
            result.MeanDifference = result.MeanA - result.MeanB;
            result.Summary = $"Insufficient data (need {MinSampleSize} samples per variant, " +
                             $"have {variantAScores.Count} and {variantBScores.Count})";
            return result;
        }

        // Compute means
        result.MeanA = ComputeMean(variantAScores);
        result.MeanB = ComputeMean(variantBScores);
        result.MeanDifference = result.MeanA - result.MeanB;

        // Compute variances
        result.VarianceA = ComputeVariance(variantAScores, result.MeanA);
        result.VarianceB = ComputeVariance(variantBScores, result.MeanB);

        // Standard deviations
        result.StandardDeviationA = Math.Sqrt(result.VarianceA);
        result.StandardDeviationB = Math.Sqrt(result.VarianceB);

        // Welch's t-test
        var tStatistic = ComputeWelchTStatistic(
            result.MeanA, result.MeanB,
            result.VarianceA, result.VarianceB,
            variantAScores.Count, variantBScores.Count);

        var degreesOfFreedom = ComputeWelchDegreesOfFreedom(
            result.VarianceA, result.VarianceB,
            variantAScores.Count, variantBScores.Count);

        // Approximate p-value using t-distribution approximation
        result.PValue = ApproximatePValue(Math.Abs(tStatistic), degreesOfFreedom);

        // Two-tailed test
        result.Significance = result.PValue < significanceLevel
            ? StatisticalSignificance.Significant
            : StatisticalSignificance.NotSignificant;

        // Confidence interval (95% for the mean difference)
        var criticalValue = GetCriticalValue(significanceLevel);
        var standardError = Math.Sqrt(
            result.VarianceA / variantAScores.Count +
            result.VarianceB / variantBScores.Count);

        result.ConfidenceIntervalLower = result.MeanDifference - criticalValue * standardError;
        result.ConfidenceIntervalUpper = result.MeanDifference + criticalValue * standardError;

        // Summary
        result.Significance switch
        {
            StatisticalSignificance.Significant =>
                result.Summary = $"Significant difference detected (p={result.PValue:F4}). " +
                                 $"Mean A={result.MeanA:F4}, Mean B={result.MeanB:F4}, " +
                                 $"Difference={result.MeanDifference:F4}",
            StatisticalSignificance.NotSignificant =>
                result.Summary = $"No significant difference detected (p={result.PValue:F4}). " +
                                 $"Mean A={result.MeanA:F4}, Mean B={result.MeanB:F4}",
            _ =>
                result.Summary = "Insufficient data for statistical analysis"
        };

        return result;
    }

    /// <summary>
    /// Computes the arithmetic mean of a sample.
    /// </summary>
    private static double ComputeMean(IReadOnlyList<double> values)
    {
        return values.Average();
    }

    /// <summary>
    /// Computes the sample variance (Bessel's correction: n-1).
    /// </summary>
    private static double ComputeVariance(IReadOnlyList<double> values, double mean)
    {
        if (values.Count <= 1) return 0;

        double sumSquaredDiffs = 0;
        foreach (var v in values)
        {
            var diff = v - mean;
            sumSquaredDiffs += diff * diff;
        }

        return sumSquaredDiffs / (values.Count - 1);
    }

    /// <summary>
    /// Computes Welch's t-statistic for unequal variances.
    /// t = (meanA - meanB) / sqrt(varA/nA + varB/nB)
    /// </summary>
    private static double ComputeWelchTStatistic(
        double meanA, double meanB,
        double varA, double varB,
        int nA, int nB)
    {
        var denominator = Math.Sqrt(varA / nA + varB / nB);

        if (denominator < 1e-10)
            return 0; // Avoid division by zero when variances are essentially zero

        return (meanA - meanB) / denominator;
    }

    /// <summary>
    /// Computes Welch–Satterthwaite degrees of freedom.
    /// df = (varA/nA + varB/nB)^2 / [(varA/nA)^2/(nA-1) + (varB/nB)^2/(nB-1)]
    /// </summary>
    private static double ComputeWelchDegreesOfFreedom(
        double varA, double varB,
        int nA, int nB)
    {
        var a = varA / nA;
        var b = varB / nB;
        var numerator = (a + b) * (a + b);
        var denominator = (a * a / (nA - 1)) + (b * b / (nB - 1));

        if (denominator < 1e-10)
            return nA + nB - 2; // Fallback to pooled df

        return numerator / denominator;
    }

    /// <summary>
    /// Approximates the two-tailed p-value from a t-statistic using
    /// a rational approximation of the incomplete beta function.
    /// This is a deterministic approximation suitable for engineering use.
    /// </summary>
    private static double ApproximatePValue(double t, double df)
    {
        if (df <= 0 || t < 0) return 1.0;

        // Use the relationship between t-distribution and beta distribution
        var x = df / (df + t * t);

        // Approximate the regularized incomplete beta function I_x(a, b)
        // where a = df/2, b = 0.5
        var a = df / 2.0;
        var b = 0.5;
        var betaInc = RegularizedIncompleteBeta(x, a, b);

        return Math.Clamp(betaInc, 0.0, 1.0);
    }

    /// <summary>
    /// Approximates the regularized incomplete beta function I_x(a, b)
    /// using a continued fraction expansion (Lentz's method).
    /// Sufficient accuracy for engineering p-value approximation.
    /// </summary>
    private static double RegularizedIncompleteBeta(double x, double a, double b)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;

        // Use the continued fraction representation
        var lnBeta = LogGamma(a) + LogGamma(b) - LogGamma(a + b);
        var front = Math.Exp(Math.Log(x) * a + Math.Log(1 - x) * b - lnBeta) / a;

        // Lentz's continued fraction
        var f = 1.0;
        var c = 1.0;
        var d = 0;

        for (int i = 0; i <= 200; i++)
        {
            double numerator;
            int m = i / 2;

            if (i == 0)
            {
                d = 1.0;
                c = 1.0;
                continue;
            }

            if (i % 2 == 0)
            {
                numerator = m * (b - m) * x / ((a + 2 * m - 1) * (a + 2 * m));
            }
            else
            {
                numerator = -((a + m) * (a + b + m) * x) / ((a + 2 * m) * (a + 2 * m + 1));
            }

            d = 1.0 + numerator * d;
            if (Math.Abs(d) < 1e-30) d = 1e-30;
            d = 1.0 / d;

            c = 1.0 + numerator / c;
            if (Math.Abs(c) < 1e-30) c = 1e-30;

            f *= c * d;

            if (Math.Abs(c * d - 1.0) < 1e-8) break;
        }

        return front * (f - 1.0);
    }

    /// <summary>
    /// Stirling's approximation for log-gamma with correction terms.
    /// </summary>
    private static double LogGamma(double x)
    {
        if (x <= 0) return double.PositiveInfinity;

        // Lanczos approximation coefficients
        double[] g = [76.18009172947146, -86.50532032941677,
                      24.01409824083091, -1.231739572450155,
                      0.001208650973866179, -0.000005395239384953];

        double y = x;
        double tmp = x + 5.5;
        tmp -= (x + 0.5) * Math.Log(tmp);
        double ser = 1.000000000190015;

        for (int j = 0; j < 6; j++)
            ser += g[j] / ++y;

        return -tmp + Math.Log(2.5066282746310005 * ser / x);
    }

    /// <summary>
    /// Gets an approximate critical value for a two-tailed test.
    /// Uses common values for engineering purposes.
    /// </summary>
    private static double GetCriticalValue(double significanceLevel)
    {
        return significanceLevel switch
        {
            <= 0.001 => 3.291,
            <= 0.01 => 2.576,
            <= 0.05 => 1.960,
            <= 0.10 => 1.645,
            _ => 1.960
        };
    }
}
