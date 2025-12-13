namespace ReadyStackGo.Infrastructure.DataAccess.Repositories;

using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;

/// <summary>
/// SQLite implementation of IHealthSnapshotRepository.
/// </summary>
public class HealthSnapshotRepository : IHealthSnapshotRepository
{
    private readonly ReadyStackGoDbContext _context;

    public HealthSnapshotRepository(ReadyStackGoDbContext context)
    {
        _context = context;
    }

    public HealthSnapshotId NextIdentity()
    {
        return HealthSnapshotId.Create();
    }

    public void Add(HealthSnapshot snapshot)
    {
        _context.HealthSnapshots.Add(snapshot);
    }

    public HealthSnapshot? Get(HealthSnapshotId id)
    {
        return _context.HealthSnapshots
            .FirstOrDefault(h => h.Id == id);
    }

    public HealthSnapshot? GetLatestForDeployment(DeploymentId deploymentId)
    {
        return _context.HealthSnapshots
            .Where(h => h.DeploymentId == deploymentId)
            .OrderByDescending(h => h.CapturedAtUtc)
            .FirstOrDefault();
    }

    public IEnumerable<HealthSnapshot> GetLatestForEnvironment(EnvironmentId environmentId)
    {
        // Get the latest snapshot for each deployment in the environment
        // Using a subquery to get the max CapturedAtUtc per deployment
        var latestSnapshots = _context.HealthSnapshots
            .Where(h => h.EnvironmentId == environmentId)
            .GroupBy(h => h.DeploymentId)
            .Select(g => g.OrderByDescending(h => h.CapturedAtUtc).First())
            .ToList();

        return latestSnapshots;
    }

    public IEnumerable<HealthSnapshot> GetHistory(DeploymentId deploymentId, int limit = 10)
    {
        return _context.HealthSnapshots
            .Where(h => h.DeploymentId == deploymentId)
            .OrderByDescending(h => h.CapturedAtUtc)
            .Take(limit)
            .ToList();
    }

    public void RemoveOlderThan(TimeSpan age)
    {
        var cutoff = DateTime.UtcNow - age;
        var oldSnapshots = _context.HealthSnapshots
            .Where(h => h.CapturedAtUtc < cutoff)
            .ToList();

        _context.HealthSnapshots.RemoveRange(oldSnapshots);
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }
}
