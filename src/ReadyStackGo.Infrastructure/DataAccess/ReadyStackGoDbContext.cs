namespace ReadyStackGo.Infrastructure.DataAccess;

using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;

/// <summary>
/// EF Core DbContext for ReadyStackGo persistence.
/// </summary>
public class ReadyStackGoDbContext : DbContext
{
    public ReadyStackGoDbContext(DbContextOptions<ReadyStackGoDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Environment> Environments => Set<Environment>();
    public DbSet<Deployment> Deployments => Set<Deployment>();
    public DbSet<HealthSnapshot> HealthSnapshots => Set<HealthSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReadyStackGoDbContext).Assembly);
    }
}
