using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.StackManagement.Sources;
using DeploymentOrganizationId = ReadyStackGo.Domain.Deployment.OrganizationId;

namespace ReadyStackGo.Application.Snmp;

/// <summary>
/// Builds a <see cref="SnmpSnapshot"/> from the domain repositories. Used by
/// the SNMP agent (via a caching proxy) and by the OID Reference UI page.
/// </summary>
public sealed class SnmpSnapshotProvider : ISnmpSnapshotProvider
{
    private static readonly DateTime ProcessStartedAt = DateTime.UtcNow;

    private readonly IProductDeploymentRepository _productDeployments;
    private readonly IEnvironmentRepository _environments;
    private readonly IOrganizationRepository _organizations;
    private readonly IStackSourceRepository _stackSources;
    private readonly IDeploymentRepository _deployments;
    private readonly string _rsgoVersion;

    public SnmpSnapshotProvider(
        IProductDeploymentRepository productDeployments,
        IEnvironmentRepository environments,
        IOrganizationRepository organizations,
        IStackSourceRepository stackSources,
        IDeploymentRepository deployments,
        string rsgoVersion)
    {
        _productDeployments = productDeployments;
        _environments = environments;
        _organizations = organizations;
        _stackSources = stackSources;
        _deployments = deployments;
        _rsgoVersion = rsgoVersion;
    }

    public SnmpSnapshot GetCurrentSnapshot()
    {
        var now = DateTime.UtcNow;
        var uptimeTicks = (long)((now - ProcessStartedAt).TotalMilliseconds * 10);

        var envEntries = BuildEnvironments();
        var environmentIndexById = envEntries.ToDictionary(e => e.EnvironmentId, e => e.EnvironmentIndex);

        var productEntries = new List<SnmpProductEntry>();
        var stackEntries = new List<SnmpStackEntry>();
        var serviceEntries = new List<SnmpServiceEntry>();

        foreach (var product in _productDeployments.GetAllActive())
        {
            var envId = product.EnvironmentId.Value.ToString();
            if (!environmentIndexById.TryGetValue(envId, out var envIdx))
            {
                continue;
            }

            var prodIdx = SnmpIndex.From(product.ProductGroupId);

            productEntries.Add(new SnmpProductEntry(
                EnvironmentIndex: envIdx,
                ProductIndex: prodIdx,
                ProductId: product.ProductId,
                Name: product.ProductDisplayName,
                Version: product.ProductVersion,
                Status: (int)product.Status,
                StatusText: product.Status.ToString(),
                OperationMode: (int)product.OperationMode,
                TotalStacks: product.TotalStacks,
                RunningStacks: product.CompletedStacks,
                FailedStacks: product.FailedStacks,
                LastDeployedAt: product.CompletedAt ?? product.CreatedAt,
                ErrorMessage: product.ErrorMessage ?? string.Empty));

            foreach (var stack in product.Stacks)
            {
                var stackIdx = SnmpIndex.From(product.ProductGroupId, stack.StackName);

                stackEntries.Add(new SnmpStackEntry(
                    EnvironmentIndex: envIdx,
                    ProductIndex: prodIdx,
                    StackIndex: stackIdx,
                    Name: stack.StackName,
                    Status: (int)stack.Status,
                    StatusText: stack.Status.ToString(),
                    ServiceCount: stack.ServiceCount,
                    Order: stack.Order,
                    ErrorMessage: stack.ErrorMessage ?? string.Empty));

                if (stack.DeploymentId is null) continue;

                var deployment = SafeGet(stack.DeploymentId);
                if (deployment is null) continue;

                foreach (var service in deployment.Services)
                {
                    var serviceIdx = SnmpIndex.From(product.ProductGroupId, stack.StackName, service.ServiceName);

                    serviceEntries.Add(new SnmpServiceEntry(
                        EnvironmentIndex: envIdx,
                        ProductIndex: prodIdx,
                        StackIndex: stackIdx,
                        ServiceIndex: serviceIdx,
                        Name: service.ServiceName,
                        ContainerName: service.ContainerName ?? string.Empty,
                        Running: string.Equals(service.Status, "running", StringComparison.OrdinalIgnoreCase),
                        HealthStatus: MapHealthStatus(service.Status),
                        RestartCount: 0,
                        LastHealthCheck: null));
                }
            }
        }

        var system = new SnmpSystemInfo(
            Version: _rsgoVersion,
            UptimeHundredthsOfSeconds: uptimeTicks,
            EnvironmentCount: envEntries.Count,
            SourceCount: CountSources(),
            DbHealthy: true,
            BuildTimestamp: ProcessStartedAt);

        return new SnmpSnapshot(system, envEntries, productEntries, stackEntries, serviceEntries, now);
    }

    private List<SnmpEnvironmentEntry> BuildEnvironments()
    {
        var entries = new List<SnmpEnvironmentEntry>();
        var organization = _organizations.GetAll().FirstOrDefault();
        if (organization is null) return entries;

        var deploymentOrgId = DeploymentOrganizationId.FromIdentityAccess(organization.Id);
        foreach (var env in _environments.GetByOrganization(deploymentOrgId))
        {
            var envId = env.Id.Value.ToString();
            entries.Add(new SnmpEnvironmentEntry(
                EnvironmentIndex: SnmpIndex.From(envId),
                EnvironmentId: envId,
                Name: env.Name,
                EnvironmentType: (int)env.Type));
        }
        return entries;
    }

    private int CountSources()
    {
        try
        {
            return _stackSources.GetAllAsync().GetAwaiter().GetResult().Count;
        }
        catch
        {
            return 0;
        }
    }

    private Deployment? SafeGet(DeploymentId id)
    {
        try { return _deployments.Get(id); }
        catch { return null; }
    }

    private static int MapHealthStatus(string status) => status?.ToLowerInvariant() switch
    {
        "running" => 1,
        "starting" => 3,
        "stopped" => 0,
        "failed" => 2,
        "removed" => 0,
        _ => 0,
    };
}
