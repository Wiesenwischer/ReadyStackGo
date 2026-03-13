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
        // Use raw SQL to efficiently get the latest snapshot per deployment.
        // The EF GroupBy+First pattern causes client-side evaluation, loading ALL rows.
        // Stale snapshots from removed deployments are cleaned up at the source
        // (DeploymentService calls RemoveForDeployment when marking a deployment as removed).
        var envId = environmentId.Value.ToString().ToUpperInvariant();

        return _context.HealthSnapshots
            .FromSqlRaw(
                """
                SELECT h.*
                FROM "HealthSnapshots" h
                INNER JOIN (
                    SELECT "DeploymentId", MAX("CapturedAtUtc") AS "MaxDate"
                    FROM "HealthSnapshots"
                    WHERE UPPER("EnvironmentId") = {0}
                    GROUP BY "DeploymentId"
                ) latest ON h."DeploymentId" = latest."DeploymentId"
                    AND h."CapturedAtUtc" = latest."MaxDate"
                WHERE UPPER(h."EnvironmentId") = {0}
                """,
                envId)
            .ToList();
    }

    public void RemoveForDeployment(DeploymentId deploymentId)
    {
        var id = deploymentId.Value.ToString().ToUpperInvariant();
        _context.Database.ExecuteSql(
            $"""
            DELETE FROM "HealthSnapshots" WHERE UPPER("DeploymentId") = {id}
            """);
    }

    public IEnumerable<HealthSnapshot> GetHistory(DeploymentId deploymentId, int limit = 10)
    {
        return _context.HealthSnapshots
            .Where(h => h.DeploymentId == deploymentId)
            .OrderByDescending(h => h.CapturedAtUtc)
            .Take(limit)
            .ToList();
    }

    public IEnumerable<HealthSnapshot> GetTransitions(DeploymentId deploymentId)
    {
        var id = deploymentId.Value.ToString().ToUpperInvariant();

        // Use LAG() window function to find rows where OverallStatus changed.
        // Also include first snapshot (PrevStatus IS NULL) and latest (RowDesc = 1).
        return _context.HealthSnapshots
            .FromSqlRaw(
                """
                WITH ranked AS (
                    SELECT "Id",
                           "OverallStatus",
                           LAG("OverallStatus") OVER (ORDER BY "CapturedAtUtc") AS "PrevStatus",
                           ROW_NUMBER() OVER (ORDER BY "CapturedAtUtc" DESC) AS "RowDesc"
                    FROM "HealthSnapshots"
                    WHERE UPPER("DeploymentId") = {0}
                )
                SELECT h.*
                FROM "HealthSnapshots" h
                INNER JOIN ranked r ON h."Id" = r."Id"
                WHERE r."PrevStatus" IS NULL
                   OR r."OverallStatus" != r."PrevStatus"
                   OR r."RowDesc" = 1
                ORDER BY h."CapturedAtUtc" ASC
                """,
                id)
            .ToList();
    }

    public int RemoveOlderThan(TimeSpan age)
    {
        var cutoff = DateTime.UtcNow - age;

        // Use ExecuteSql to delete directly in the database without loading entities.
        // EF Core's interpolated SQL handles DateTime parameter formatting for SQLite.
        return _context.Database.ExecuteSql(
            $"""
            DELETE FROM "HealthSnapshots" WHERE "CapturedAtUtc" < {cutoff}
            """);
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }
}
