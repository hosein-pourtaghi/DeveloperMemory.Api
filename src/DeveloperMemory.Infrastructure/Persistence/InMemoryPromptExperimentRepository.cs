using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// In-memory implementation of IPromptExperimentRepository for testing.
/// Thread-safe for concurrent test execution.
/// </summary>
public class InMemoryPromptExperimentRepository : IPromptExperimentRepository
{
    private readonly Dictionary<Guid, PromptExperiment> _experiments = [];
    private readonly Dictionary<Guid, List<PromptExperimentVariant>> _variants = [];
    private readonly List<PromptExperimentAssignment> _assignments = [];
    private readonly List<PromptExperimentResult> _results = [];
    private readonly object _lock = new();

    public Task<PromptExperiment> CreateExperimentAsync(PromptExperiment experiment, CancellationToken ct = default)
    {
        experiment.Id = experiment.Id == Guid.Empty ? Guid.NewGuid() : experiment.Id;
        experiment.CreatedAt = DateTime.UtcNow;
        experiment.UpdatedAt = DateTime.UtcNow;
        experiment.Status = ExperimentStatus.Draft;

        lock (_lock)
        {
            _experiments[experiment.Id] = experiment;
            _variants[experiment.Id] = [];
        }

        return Task.FromResult(experiment);
    }

    public Task<PromptExperiment?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _experiments.TryGetValue(id, out var experiment);
            return Task.FromResult(experiment);
        }
    }

    public Task<PromptExperiment?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var experiment = _experiments.Values.FirstOrDefault(e => e.Name == name);
            return Task.FromResult(experiment);
        }
    }

    public Task<IReadOnlyList<PromptExperiment>> ListAsync(ExperimentStatus? status = null, CancellationToken ct = default)
    {
        lock (_lock)
        {
            IReadOnlyList<PromptExperiment> result = status.HasValue
                ? _experiments.Values.Where(e => e.Status == status.Value).OrderByDescending(e => e.CreatedAt).ToList()
                : _experiments.Values.OrderByDescending(e => e.CreatedAt).ToList();
            return Task.FromResult(result);
        }
    }

    public Task UpdateAsync(PromptExperiment experiment, CancellationToken ct = default)
    {
        experiment.UpdatedAt = DateTime.UtcNow;
        lock (_lock)
        {
            _experiments[experiment.Id] = experiment;
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_experiments.ContainsKey(id));
        }
    }

    public Task<IReadOnlyList<PromptExperiment>> GetRunningAsync(CancellationToken ct = default)
    {
        return ListAsync(ExperimentStatus.Running, ct);
    }

    public Task<PromptExperimentVariant> AddVariantAsync(PromptExperimentVariant variant, CancellationToken ct = default)
    {
        variant.Id = variant.Id == Guid.Empty ? Guid.NewGuid() : variant.Id;
        variant.CreatedAt = DateTime.UtcNow;

        lock (_lock)
        {
            if (!_variants.ContainsKey(variant.ExperimentId))
                _variants[variant.ExperimentId] = [];
            _variants[variant.ExperimentId].Add(variant);
        }

        return Task.FromResult(variant);
    }

    public Task UpdateVariantAsync(PromptExperimentVariant variant, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_variants.TryGetValue(variant.ExperimentId, out var list))
            {
                var index = list.FindIndex(v => v.Id == variant.Id);
                if (index >= 0) list[index] = variant;
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PromptExperimentVariant>> GetVariantsAsync(Guid experimentId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            IReadOnlyList<PromptExperimentVariant> result = _variants.TryGetValue(experimentId, out var list)
                ? list.OrderBy(v => v.Name).ToList()
                : [];
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<PromptExperimentVariant>> GetEnabledVariantsAsync(Guid experimentId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            IReadOnlyList<PromptExperimentVariant> result = _variants.TryGetValue(experimentId, out var list)
                ? list.Where(v => v.Enabled).OrderBy(v => v.Name).ToList()
                : [];
            return Task.FromResult(result);
        }
    }

    public Task SetVariantEnabledAsync(Guid variantId, bool enabled, CancellationToken ct = default)
    {
        lock (_lock)
        {
            foreach (var variants in _variants.Values)
            {
                var variant = variants.FirstOrDefault(v => v.Id == variantId);
                if (variant != null)
                {
                    variant.Enabled = enabled;
                    break;
                }
            }
        }
        return Task.CompletedTask;
    }

    public Task<PromptExperimentAssignment?> GetAssignmentAsync(Guid experimentId, string assignmentKeyHash, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var assignment = _assignments.FirstOrDefault(a =>
                a.ExperimentId == experimentId &&
                a.AssignmentKeyHash == assignmentKeyHash);
            return Task.FromResult(assignment);
        }
    }

    public Task<PromptExperimentAssignment> CreateAssignmentAsync(PromptExperimentAssignment assignment, CancellationToken ct = default)
    {
        assignment.Id = assignment.Id == Guid.Empty ? Guid.NewGuid() : assignment.Id;
        assignment.CreatedAt = DateTime.UtcNow;

        lock (_lock)
        {
            // Check for existing (concurrent duplicate)
            var existing = _assignments.FirstOrDefault(a =>
                a.ExperimentId == assignment.ExperimentId &&
                a.AssignmentKeyHash == assignment.AssignmentKeyHash);

            if (existing != null)
                return Task.FromResult(existing);

            _assignments.Add(assignment);
        }

        return Task.FromResult(assignment);
    }

    public Task<PromptExperimentResult> RecordResultAsync(PromptExperimentResult result, CancellationToken ct = default)
    {
        result.Id = result.Id == Guid.Empty ? Guid.NewGuid() : result.Id;
        result.CreatedAt = DateTime.UtcNow;

        lock (_lock)
        {
            _results.Add(result);
        }

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<PromptExperimentResult>> GetResultsAsync(Guid experimentId, Guid? variantId = null, CancellationToken ct = default)
    {
        lock (_lock)
        {
            IReadOnlyList<PromptExperimentResult> result = variantId.HasValue
                ? _results.Where(r => r.ExperimentId == experimentId && r.VariantId == variantId.Value)
                    .OrderByDescending(r => r.CreatedAt).ToList()
                : _results.Where(r => r.ExperimentId == experimentId)
                    .OrderByDescending(r => r.CreatedAt).ToList();
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<PromptExperimentResult>> GetResultsByTimeRangeAsync(
        Guid experimentId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        lock (_lock)
        {
            IReadOnlyList<PromptExperimentResult> result = _results
                .Where(r => r.ExperimentId == experimentId && r.CreatedAt >= from && r.CreatedAt <= to)
                .OrderByDescending(r => r.CreatedAt).ToList();
            return Task.FromResult(result);
        }
    }
}
