using MediatR;
using Microsoft.Extensions.Options;

namespace ReadyStackGo.Application.Snmp;

public sealed class GetOidReferenceHandler : IRequestHandler<GetOidReferenceQuery, OidReferenceResult>
{
    private readonly ISnmpSnapshotProvider _snapshotProvider;
    private readonly SnmpAgentOptions _options;

    public GetOidReferenceHandler(
        ISnmpSnapshotProvider snapshotProvider,
        IOptions<SnmpAgentOptions> options)
    {
        _snapshotProvider = snapshotProvider;
        _options = options.Value;
    }

    public Task<OidReferenceResult> Handle(GetOidReferenceQuery request, CancellationToken cancellationToken)
    {
        var snapshot = _snapshotProvider.GetCurrentSnapshot();
        var root = _options.RootOid;

        var systemScalars = new List<OidReferenceScalar>
        {
            new("rsgoSystemVersion",          $"{root}.1.1.0", "STRING",      snapshot.System.Version),
            new("rsgoSystemUptime",           $"{root}.1.2.0", "TimeTicks",   snapshot.System.UptimeHundredthsOfSeconds.ToString()),
            new("rsgoSystemEnvironmentCount", $"{root}.1.3.0", "Integer32",   snapshot.System.EnvironmentCount.ToString()),
            new("rsgoSystemSourceCount",      $"{root}.1.4.0", "Integer32",   snapshot.System.SourceCount.ToString()),
            new("rsgoSystemDbHealth",         $"{root}.1.5.0", "Integer32",   snapshot.System.DbHealthy ? "1 (up)" : "0 (down)"),
            new("rsgoSystemBuildTimestamp",   $"{root}.1.6.0", "DateAndTime", snapshot.System.BuildTimestamp.ToString("O")),
        };

        var environments = new List<OidReferenceEnvironment>();
        foreach (var env in snapshot.Environments)
        {
            var envBaseOid = $"{root}.2.1.<column>.{env.EnvironmentIndex}";
            var products = snapshot.Products
                .Where(p => p.EnvironmentIndex == env.EnvironmentIndex)
                .Select(p => BuildProduct(root, snapshot, p))
                .ToList();

            environments.Add(new OidReferenceEnvironment(
                env.EnvironmentIndex,
                env.EnvironmentId,
                env.Name,
                env.EnvironmentType,
                envBaseOid,
                products));
        }

        return Task.FromResult(new OidReferenceResult(
            RootOid: root,
            SnmpEnabled: _options.Enabled,
            Port: _options.Port,
            ListenAddress: _options.ListenAddress,
            System: systemScalars,
            Environments: environments));
    }

    private static OidReferenceProduct BuildProduct(string root, SnmpSnapshot snapshot, SnmpProductEntry p)
    {
        var baseOid = $"{root}.3.1.<column>.{p.EnvironmentIndex}.{p.ProductIndex}";
        var stacks = snapshot.Stacks
            .Where(s => s.EnvironmentIndex == p.EnvironmentIndex && s.ProductIndex == p.ProductIndex)
            .Select(s => BuildStack(root, snapshot, s))
            .ToList();

        return new OidReferenceProduct(p.ProductIndex, p.ProductId, p.Name, p.Version,
            p.Status, p.StatusText, baseOid, stacks);
    }

    private static OidReferenceStack BuildStack(string root, SnmpSnapshot snapshot, SnmpStackEntry s)
    {
        var baseOid = $"{root}.4.1.<column>.{s.EnvironmentIndex}.{s.ProductIndex}.{s.StackIndex}";
        var services = snapshot.Services
            .Where(sv => sv.EnvironmentIndex == s.EnvironmentIndex
                      && sv.ProductIndex == s.ProductIndex
                      && sv.StackIndex == s.StackIndex)
            .Select(sv => new OidReferenceService(
                sv.ServiceIndex,
                sv.Name,
                sv.ContainerName,
                sv.Running,
                $"{root}.5.1.<column>.{sv.EnvironmentIndex}.{sv.ProductIndex}.{sv.StackIndex}.{sv.ServiceIndex}"))
            .ToList();

        return new OidReferenceStack(s.StackIndex, s.Name, s.Status, s.StatusText, baseOid, services);
    }
}
