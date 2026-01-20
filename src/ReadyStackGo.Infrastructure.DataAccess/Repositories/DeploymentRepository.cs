namespace ReadyStackGo.Infrastructure.DataAccess.Repositories;

using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

/// <summary>
/// SQLite implementation of IDeploymentRepository.
/// </summary>
public class DeploymentRepository : IDeploymentRepository
{
    private readonly ReadyStackGoDbContext _context;

    public DeploymentRepository(ReadyStackGoDbContext context)
    {
        _context = context;
    }

    public DeploymentId NextIdentity()
    {
        return DeploymentId.Create();
    }

    public void Add(Deployment deployment)
    {
        _context.Deployments.Add(deployment);
    }

    public void Update(Deployment deployment)
    {
        _context.Deployments.Update(deployment);
    }

    public Deployment? Get(DeploymentId id)
    {
        // Owned entities (Services) are loaded automatically by EF Core
        return _context.Deployments
            .FirstOrDefault(d => d.Id == id);
    }

    public Deployment? GetById(DeploymentId id)
    {
        // Alias for Get - owned entities (Services) are loaded automatically
        return Get(id);
    }

    public IEnumerable<Deployment> GetByEnvironment(EnvironmentId environmentId)
    {
        // Owned entities (Services) are loaded automatically by EF Core
        return _context.Deployments
            .Where(d => d.EnvironmentId == environmentId)
            .OrderByDescending(d => d.CreatedAt)
            .ToList();
    }

    public Deployment? GetByStackName(EnvironmentId environmentId, string stackName)
    {
        // Owned entities (Services) are loaded automatically by EF Core
        // Return the most recent non-removed deployment with this stack name
        return _context.Deployments
            .Where(d => d.EnvironmentId == environmentId && d.StackName == stackName)
            .Where(d => d.Status != DeploymentStatus.Removed)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefault();
    }

    public void Remove(Deployment deployment)
    {
        _context.Deployments.Remove(deployment);
    }

    public IEnumerable<Deployment> GetAllActive()
    {
        return _context.Deployments
            .Where(d => d.Status == DeploymentStatus.Running || d.Status == DeploymentStatus.Stopped)
            .OrderByDescending(d => d.CreatedAt)
            .ToList();
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }
}
