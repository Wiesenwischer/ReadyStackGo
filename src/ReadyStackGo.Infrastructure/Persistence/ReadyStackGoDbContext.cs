namespace ReadyStackGo.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReadyStackGoDbContext).Assembly);
    }
}
