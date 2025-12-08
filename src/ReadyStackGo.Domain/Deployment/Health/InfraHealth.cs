namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Infrastructure health including databases, disks, and external services.
/// </summary>
public sealed class InfraHealth : ValueObject
{
    private readonly List<DatabaseHealth> _databases;
    private readonly List<DiskHealth> _disks;
    private readonly List<ExternalServiceHealth> _externalServices;

    /// <summary>
    /// Health status of database connections.
    /// </summary>
    public IReadOnlyList<DatabaseHealth> Databases => _databases.AsReadOnly();

    /// <summary>
    /// Health status of disk mounts.
    /// </summary>
    public IReadOnlyList<DiskHealth> Disks => _disks.AsReadOnly();

    /// <summary>
    /// Health status of external service dependencies.
    /// </summary>
    public IReadOnlyList<ExternalServiceHealth> ExternalServices => _externalServices.AsReadOnly();

    /// <summary>
    /// Aggregated health status based on all infrastructure components.
    /// </summary>
    public HealthStatus Status { get; }

    private InfraHealth(
        IEnumerable<DatabaseHealth>? databases,
        IEnumerable<DiskHealth>? disks,
        IEnumerable<ExternalServiceHealth>? externalServices)
    {
        _databases = databases?.ToList() ?? new List<DatabaseHealth>();
        _disks = disks?.ToList() ?? new List<DiskHealth>();
        _externalServices = externalServices?.ToList() ?? new List<ExternalServiceHealth>();
        Status = CalculateAggregatedStatus();
    }

    public static InfraHealth Create(
        IEnumerable<DatabaseHealth>? databases = null,
        IEnumerable<DiskHealth>? disks = null,
        IEnumerable<ExternalServiceHealth>? externalServices = null)
    {
        return new InfraHealth(databases, disks, externalServices);
    }

    public static InfraHealth Empty()
    {
        return new InfraHealth(null, null, null);
    }

    /// <summary>
    /// Indicates if any infrastructure component has issues.
    /// </summary>
    public bool HasIssues => Status.RequiresAttention;

    /// <summary>
    /// Gets all unhealthy components.
    /// </summary>
    public IEnumerable<string> GetUnhealthyComponents()
    {
        foreach (var db in _databases.Where(d => d.Status.RequiresAttention))
            yield return $"Database: {db.Id}";

        foreach (var disk in _disks.Where(d => d.Status.RequiresAttention))
            yield return $"Disk: {disk.Mount}";

        foreach (var svc in _externalServices.Where(s => s.Status.RequiresAttention))
            yield return $"Service: {svc.Id}";
    }

    private HealthStatus CalculateAggregatedStatus()
    {
        var allStatuses = new List<HealthStatus>();

        allStatuses.AddRange(_databases.Select(d => d.Status));
        allStatuses.AddRange(_disks.Select(d => d.Status));
        allStatuses.AddRange(_externalServices.Select(s => s.Status));

        if (!allStatuses.Any())
            return HealthStatus.Unknown;

        return HealthStatus.Aggregate(allStatuses);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Status;
        foreach (var db in _databases.OrderBy(d => d.Id))
            yield return db;
        foreach (var disk in _disks.OrderBy(d => d.Mount))
            yield return disk;
        foreach (var svc in _externalServices.OrderBy(s => s.Id))
            yield return svc;
    }
}
