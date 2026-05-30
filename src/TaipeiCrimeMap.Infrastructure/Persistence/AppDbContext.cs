using Microsoft.EntityFrameworkCore;
using TaipeiCrimeMap.Domain.Aggregates;

namespace TaipeiCrimeMap.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly DbContextOptions<AppDbContext> _options;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        _options = options;
    }

    public DbSet<TheftCase> TheftCases => Set<TheftCase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}