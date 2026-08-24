using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeveloperMemory.Infrastructure.Persistence;

public class DeveloperMemoryDbContext : DbContext
{
    public DbSet<MemoryEntry> MemoryEntries => Set<MemoryEntry>();
    public DbSet<Project> Projects => Set<Project>();

    public DeveloperMemoryDbContext(DbContextOptions<DeveloperMemoryDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeveloperMemoryDbContext).Assembly);
    }
}
