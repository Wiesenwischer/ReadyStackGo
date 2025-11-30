namespace ReadyStackGo.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.Identity.Aggregates;
using ReadyStackGo.Domain.Access.Aggregates;

/// <summary>
/// EF Core DbContext for ReadyStackGo persistence.
/// </summary>
public class ReadyStackGoDbContext : DbContext
{
    public ReadyStackGoDbContext(DbContextOptions<ReadyStackGoDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReadyStackGoDbContext).Assembly);
    }
}
