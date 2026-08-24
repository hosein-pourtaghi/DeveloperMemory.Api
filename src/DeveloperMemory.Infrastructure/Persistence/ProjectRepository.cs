using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeveloperMemory.Infrastructure.Persistence;

public class ProjectRepository : IProjectRepository
{
    private readonly DeveloperMemoryDbContext _context;

    public ProjectRepository(DeveloperMemoryDbContext context)
    {
        _context = context;
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<List<Project>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Projects
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<Project> CreateAsync(Project project, CancellationToken ct = default)
    {
        _context.Projects.Add(project);
        await _context.SaveChangesAsync(ct);
        return project;
    }

    public async Task<Project> UpdateAsync(Project project, CancellationToken ct = default)
    {
        _context.Projects.Update(project);
        await _context.SaveChangesAsync(ct);
        return project;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _context.Projects.FindAsync([id], ct);
        if (project == null) return false;

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
