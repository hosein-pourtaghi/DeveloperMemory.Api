using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of prompt experiment repository.
/// Provides real database-backed experiment, variant, and assignment operations.
/// </summary>
public class PromptExperimentRepository : IPromptExperimentRepository
{
    private readonly DeveloperMemoryDbContext _context;
    private readonly ILogger<PromptExperimentRepository> _logger;

    public PromptExperimentRepository(
        DeveloperMemoryDbContext context,
        ILogger<PromptExperimentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ── Experiment operations ──

    public async Task<PromptExperiment> CreateExperimentAsync(PromptExperiment experiment, CancellationToken ct = default)
    {
        experiment.Id = experiment.Id == Guid.Empty ? Guid.NewGuid() : experiment.Id;
        experiment.CreatedAt = DateTime.UtcNow;
        experiment.UpdatedAt = DateTime.UtcNow;
        experiment.Status = ExperimentStatus.Draft;

        _context.PromptExperiments.Add(experiment);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Experiment created: {Name} ({Id})", experiment.Name, experiment.Id);
        return experiment;
    }

    public async Task<PromptExperiment?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.PromptExperiments.FindAsync([id], ct);
    }

    public async Task<PromptExperiment?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await _context.PromptExperiments
            .FirstOrDefaultAsync(e => e.Name == name, ct);
    }

    public async Task<IReadOnlyList<PromptExperiment>> ListAsync(ExperimentStatus? status = null, CancellationToken ct = default)
    {
        IQueryable<PromptExperiment> query = _context.PromptExperiments;

        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(PromptExperiment experiment, CancellationToken ct = default)
    {
        experiment.UpdatedAt = DateTime.UtcNow;
        _context.PromptExperiments.Update(experiment);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.PromptExperiments.AnyAsync(e => e.Id == id, ct);
    }

    public async Task<IReadOnlyList<PromptExperiment>> GetRunningAsync(CancellationToken ct = default)
    {
        return await _context.PromptExperiments
            .Where(e => e.Status == ExperimentStatus.Running)
            .ToListAsync(ct);
    }

    // ── Variant operations ──

    public async Task<PromptExperimentVariant> AddVariantAsync(PromptExperimentVariant variant, CancellationToken ct = default)
    {
        variant.Id = variant.Id == Guid.Empty ? Guid.NewGuid() : variant.Id;
        variant.CreatedAt = DateTime.UtcNow;

        _context.PromptExperimentVariants.Add(variant);
        await _context.SaveChangesAsync(ct);

        return variant;
    }

    public async Task UpdateVariantAsync(PromptExperimentVariant variant, CancellationToken ct = default)
    {
        _context.PromptExperimentVariants.Update(variant);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PromptExperimentVariant>> GetVariantsAsync(Guid experimentId, CancellationToken ct = default)
    {
        return await _context.PromptExperimentVariants
            .Where(v => v.ExperimentId == experimentId)
            .OrderBy(v => v.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PromptExperimentVariant>> GetEnabledVariantsAsync(Guid experimentId, CancellationToken ct = default)
    {
        return await _context.PromptExperimentVariants
            .Where(v => v.ExperimentId == experimentId && v.Enabled)
            .OrderBy(v => v.Name)
            .ToListAsync(ct);
    }

    public async Task SetVariantEnabledAsync(Guid variantId, bool enabled, CancellationToken ct = default)
    {
        var variant = await _context.PromptExperimentVariants.FindAsync([variantId], ct);
        if (variant != null)
        {
            variant.Enabled = enabled;
            await _context.SaveChangesAsync(ct);
        }
    }

    // ── Assignment operations ──

    public async Task<PromptExperimentAssignment?> GetAssignmentAsync(
        Guid experimentId, string assignmentKeyHash, CancellationToken ct = default)
    {
        return await _context.PromptExperimentAssignments
            .FirstOrDefaultAsync(a =>
                a.ExperimentId == experimentId &&
                a.AssignmentKeyHash == assignmentKeyHash, ct);
    }

    public async Task<PromptExperimentAssignment> CreateAssignmentAsync(
        PromptExperimentAssignment assignment, CancellationToken ct = default)
    {
        assignment.Id = assignment.Id == Guid.Empty ? Guid.NewGuid() : assignment.Id;
        assignment.CreatedAt = DateTime.UtcNow;

        _context.PromptExperimentAssignments.Add(assignment);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique constraint violation — assignment already exists (concurrent request).
            // Return the existing assignment instead of failing.
            var existing = await GetAssignmentAsync(
                assignment.ExperimentId, assignment.AssignmentKeyHash, ct);

            if (existing != null)
            {
                _logger.LogDebug(
                    "Concurrent assignment detected for experiment {ExperimentId}, returning existing",
                    assignment.ExperimentId);
                return existing;
            }

            throw;
        }

        return assignment;
    }

    // ── Result operations ──

    public async Task<PromptExperimentResult> RecordResultAsync(
        PromptExperimentResult result, CancellationToken ct = default)
    {
        result.Id = result.Id == Guid.Empty ? Guid.NewGuid() : result.Id;
        result.CreatedAt = DateTime.UtcNow;

        _context.PromptExperimentResults.Add(result);
        await _context.SaveChangesAsync(ct);

        _logger.LogDebug(
            "Experiment result recorded: Experiment={ExperimentId}, Variant={VariantId}, Quality={QualityScore}",
            result.ExperimentId, result.VariantId, result.QualityScore);

        return result;
    }

    public async Task<IReadOnlyList<PromptExperimentResult>> GetResultsAsync(
        Guid experimentId, Guid? variantId = null, CancellationToken ct = default)
    {
        IQueryable<PromptExperimentResult> query = _context.PromptExperimentResults
            .Where(r => r.ExperimentId == experimentId);

        if (variantId.HasValue)
            query = query.Where(r => r.VariantId == variantId.Value);

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PromptExperimentResult>> GetResultsByTimeRangeAsync(
        Guid experimentId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await _context.PromptExperimentResults
            .Where(r => r.ExperimentId == experimentId &&
                        r.CreatedAt >= from && r.CreatedAt <= to)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }
}
