namespace ReadyStackGo.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
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
        return _context.Deployments
            .FirstOrDefault(d => d.EnvironmentId == environmentId && d.StackName == stackName);
    }

    public void Remove(Deployment deployment)
    {
        _context.Deployments.Remove(deployment);
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }
}
