namespace ReadyStackGo.Infrastructure.DataAccess;

using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.IdentityAccess.ApiKeys;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.Deployment.Registries;
using ReadyStackGo.Domain.SharedKernel;
using ReadyStackGo.Domain.StackManagement.Sources;
using RuntimeConfig = ReadyStackGo.Domain.Deployment.RuntimeConfig;

/// <summary>
/// EF Core DbContext for ReadyStackGo persistence.
/// </summary>
public class ReadyStackGoDbContext : DbContext
{
    private readonly IDomainEventDispatcher? _domainEventDispatcher;
    private bool _isDispatching;

    public ReadyStackGoDbContext(DbContextOptions<ReadyStackGoDbContext> options)
        : base(options)
    {
    }

    public ReadyStackGoDbContext(
        DbContextOptions<ReadyStackGoDbContext> options,
        IDomainEventDispatcher domainEventDispatcher)
        : base(options)
    {
        _domainEventDispatcher = domainEventDispatcher;
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Environment> Environments => Set<Environment>();
    public DbSet<EnvironmentVariable> EnvironmentVariables => Set<EnvironmentVariable>();
    public DbSet<Deployment> Deployments => Set<Deployment>();
    public DbSet<HealthSnapshot> HealthSnapshots => Set<HealthSnapshot>();
    public DbSet<Registry> Registries => Set<Registry>();
    public DbSet<StackSource> StackSources => Set<StackSource>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<ProductDeployment> ProductDeployments => Set<ProductDeployment>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (_isDispatching || _domainEventDispatcher is null)
            return base.SaveChanges(acceptAllChangesOnSuccess);

        var events = CollectDomainEvents();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);

        if (events.Count > 0)
        {
            _isDispatching = true;
            try
            {
                _domainEventDispatcher.DispatchEventsAsync(events).GetAwaiter().GetResult();
            }
            finally
            {
                _isDispatching = false;
            }
        }

        return result;
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        if (_isDispatching || _domainEventDispatcher is null)
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        var events = CollectDomainEvents();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        if (events.Count > 0)
        {
            _isDispatching = true;
            try
            {
                await _domainEventDispatcher.DispatchEventsAsync(events, cancellationToken);
            }
            finally
            {
                _isDispatching = false;
            }
        }

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReadyStackGoDbContext).Assembly);

        // Ignore ServiceHealthCheckConfig - it's stored as JSON, not as a separate entity
        modelBuilder.Ignore<RuntimeConfig.ServiceHealthCheckConfig>();

        // Ignore ProductDeploymentPhaseRecord - it's stored as JSON, not as a separate entity
        modelBuilder.Ignore<ProductDeploymentPhaseRecord>();
    }

    private List<IDomainEvent> CollectDomainEvents()
    {
        var entities = ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var events = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }

        return events;
    }
}
