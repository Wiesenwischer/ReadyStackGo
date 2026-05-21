using System.Globalization;
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
            new("rsgoSystemUptime",           $"{root}.1.2.0", "TimeTicks",   snapshot.System.UptimeHundredthsOfSeconds.ToString(CultureInfo.InvariantCulture)),
            new("rsgoSystemEnvironmentCount", $"{root}.1.3.0", "Integer32",   snapshot.System.EnvironmentCount.ToString(CultureInfo.InvariantCulture)),
            new("rsgoSystemSourceCount",      $"{root}.1.4.0", "Integer32",   snapshot.System.SourceCount.ToString(CultureInfo.InvariantCulture)),
            new("rsgoSystemDbHealth",         $"{root}.1.5.0", "Integer32",   snapshot.System.DbHealthy ? "1 (up)" : "0 (down)"),
            new("rsgoSystemBuildTimestamp",   $"{root}.1.6.0", "DateAndTime", snapshot.System.BuildTimestamp.ToString("O", CultureInfo.InvariantCulture)),
        };

        var environments = new List<OidReferenceEnvironment>();
        foreach (var env in snapshot.Environments)
        {
            var products = snapshot.Products
                .Where(p => p.EnvironmentIndex == env.EnvironmentIndex)
                .Select(p => BuildProduct(root, snapshot, p))
                .ToList();

            environments.Add(new OidReferenceEnvironment(
                env.EnvironmentIndex,
                env.EnvironmentId,
                env.Name,
                env.EnvironmentType,
                BuildEnvironmentColumns(root, env),
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

    // Column layouts mirror OidTreeBuilder.AddEnvironments / AddProducts / AddStacks / AddServices.
    // Keep both in sync — divergence means the UI lies about what SNMP actually serves.

    private static IReadOnlyList<OidReferenceColumn> BuildEnvironmentColumns(string root, SnmpEnvironmentEntry env)
    {
        var rowSuffix = $"{env.EnvironmentIndex}";
        return new List<OidReferenceColumn>
        {
            Col(root, 2, 1, "rsgoEnvironmentIndex", "Integer32", env.EnvironmentIndex, rowSuffix),
            Col(root, 2, 2, "rsgoEnvironmentId",    "STRING",    env.EnvironmentId,    rowSuffix),
            Col(root, 2, 3, "rsgoEnvironmentName",  "STRING",    env.Name,             rowSuffix),
            Col(root, 2, 4, "rsgoEnvironmentType",  "Integer32", env.EnvironmentType,  rowSuffix),
        };
    }

    private static OidReferenceProduct BuildProduct(string root, SnmpSnapshot snapshot, SnmpProductEntry p)
    {
        var rowSuffix = $"{p.EnvironmentIndex}.{p.ProductIndex}";
        var columns = new List<OidReferenceColumn>
        {
            Col(root, 3, 1,  "rsgoProductEnvIndex",       "Integer32",   p.EnvironmentIndex,                          rowSuffix),
            Col(root, 3, 2,  "rsgoProductIndex",          "Integer32",   p.ProductIndex,                              rowSuffix),
            Col(root, 3, 3,  "rsgoProductId",             "STRING",      p.ProductId,                                 rowSuffix),
            Col(root, 3, 4,  "rsgoProductName",           "STRING",      p.Name,                                      rowSuffix),
            Col(root, 3, 5,  "rsgoProductVersion",        "STRING",      p.Version,                                   rowSuffix),
            Col(root, 3, 6,  "rsgoProductStatus",         "Integer32",   $"{p.Status} ({p.StatusText})",              rowSuffix),
            Col(root, 3, 7,  "rsgoProductStatusText",     "STRING",      p.StatusText,                                rowSuffix),
            Col(root, 3, 8,  "rsgoProductOperationMode",  "Integer32",   p.OperationMode,                             rowSuffix),
            Col(root, 3, 9,  "rsgoProductTotalStacks",    "Integer32",   p.TotalStacks,                               rowSuffix),
            Col(root, 3, 10, "rsgoProductRunningStacks",  "Integer32",   p.RunningStacks,                             rowSuffix),
            Col(root, 3, 11, "rsgoProductFailedStacks",   "Integer32",   p.FailedStacks,                              rowSuffix),
            Col(root, 3, 12, "rsgoProductLastDeployedAt", "DateAndTime", FormatDate(p.LastDeployedAt),                rowSuffix),
            Col(root, 3, 13, "rsgoProductErrorMessage",   "STRING",      p.ErrorMessage,                              rowSuffix),
        };

        var stacks = snapshot.Stacks
            .Where(s => s.EnvironmentIndex == p.EnvironmentIndex && s.ProductIndex == p.ProductIndex)
            .Select(s => BuildStack(root, snapshot, s))
            .ToList();

        return new OidReferenceProduct(
            p.ProductIndex,
            p.ProductId,
            p.Name,
            p.Version,
            p.Status,
            p.StatusText,
            columns,
            stacks);
    }

    private static OidReferenceStack BuildStack(string root, SnmpSnapshot snapshot, SnmpStackEntry s)
    {
        var rowSuffix = $"{s.EnvironmentIndex}.{s.ProductIndex}.{s.StackIndex}";
        var columns = new List<OidReferenceColumn>
        {
            Col(root, 4, 1, "rsgoStackEnvIndex",     "Integer32", s.EnvironmentIndex,                rowSuffix),
            Col(root, 4, 2, "rsgoStackProdIndex",    "Integer32", s.ProductIndex,                    rowSuffix),
            Col(root, 4, 3, "rsgoStackIndex",        "Integer32", s.StackIndex,                      rowSuffix),
            Col(root, 4, 4, "rsgoStackName",         "STRING",    s.Name,                            rowSuffix),
            Col(root, 4, 5, "rsgoStackStatus",       "Integer32", $"{s.Status} ({s.StatusText})",    rowSuffix),
            Col(root, 4, 6, "rsgoStackStatusText",   "STRING",    s.StatusText,                      rowSuffix),
            Col(root, 4, 7, "rsgoStackServiceCount", "Integer32", s.ServiceCount,                    rowSuffix),
            Col(root, 4, 8, "rsgoStackOrder",        "Integer32", s.Order,                           rowSuffix),
            Col(root, 4, 9, "rsgoStackErrorMessage", "STRING",    s.ErrorMessage,                    rowSuffix),
        };

        var services = snapshot.Services
            .Where(sv => sv.EnvironmentIndex == s.EnvironmentIndex
                      && sv.ProductIndex == s.ProductIndex
                      && sv.StackIndex == s.StackIndex)
            .Select(sv => BuildService(root, sv))
            .ToList();

        return new OidReferenceStack(
            s.StackIndex,
            s.Name,
            s.Status,
            s.StatusText,
            columns,
            services);
    }

    private static OidReferenceService BuildService(string root, SnmpServiceEntry sv)
    {
        var rowSuffix = $"{sv.EnvironmentIndex}.{sv.ProductIndex}.{sv.StackIndex}.{sv.ServiceIndex}";
        var columns = new List<OidReferenceColumn>
        {
            Col(root, 5, 1,  "rsgoServiceEnvIndex",        "Integer32",   sv.EnvironmentIndex,                rowSuffix),
            Col(root, 5, 2,  "rsgoServiceProdIndex",       "Integer32",   sv.ProductIndex,                    rowSuffix),
            Col(root, 5, 3,  "rsgoServiceStackIndex",      "Integer32",   sv.StackIndex,                      rowSuffix),
            Col(root, 5, 4,  "rsgoServiceIndex",           "Integer32",   sv.ServiceIndex,                    rowSuffix),
            Col(root, 5, 5,  "rsgoServiceName",            "STRING",      sv.Name,                            rowSuffix),
            Col(root, 5, 6,  "rsgoServiceContainerName",   "STRING",      sv.ContainerName,                   rowSuffix),
            Col(root, 5, 7,  "rsgoServiceRunning",         "Integer32",   sv.Running ? "1 (yes)" : "0 (no)",  rowSuffix),
            Col(root, 5, 8,  "rsgoServiceHealthStatus",    "Integer32",   sv.HealthStatus,                    rowSuffix),
            Col(root, 5, 9,  "rsgoServiceRestartCount",    "Counter32",   sv.RestartCount,                    rowSuffix),
            Col(root, 5, 10, "rsgoServiceLastHealthCheck", "DateAndTime", FormatDate(sv.LastHealthCheck),     rowSuffix),
        };

        return new OidReferenceService(
            sv.ServiceIndex,
            sv.Name,
            sv.ContainerName,
            sv.Running,
            columns);
    }

    private static OidReferenceColumn Col(string root, int table, int column, string symbol, string type, object currentValue, string rowSuffix)
        => new(
            Symbol: symbol,
            ColumnNumber: column,
            Oid: $"{root}.{table}.1.{column}.{rowSuffix}",
            Type: type,
            CurrentValue: currentValue?.ToString() ?? string.Empty);

    private static string FormatDate(DateTime? value)
        => value is null || value == DateTime.MinValue
            ? string.Empty
            : value.Value.ToString("O", CultureInfo.InvariantCulture);
}
