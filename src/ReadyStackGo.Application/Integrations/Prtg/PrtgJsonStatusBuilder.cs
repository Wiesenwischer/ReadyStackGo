using System.Globalization;
using ReadyStackGo.Application.Snmp;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.Integrations.Prtg;

/// <summary>
/// Pure builder that turns a <see cref="SnmpSnapshot"/> into the PRTG JSON
/// envelope. Channel values are aggregated globally — a single PRTG "HTTP
/// Data Advanced" sensor gets one row per channel. The detailed per-stack
/// view stays the domain of Variant 1 (Device Template Bundle).
///
/// Channel count is intentionally well below PRTG's 50-channel-per-sensor
/// limit — there is room to grow without splitting into multiple sensors.
/// </summary>
public sealed class PrtgJsonStatusBuilder : IPrtgJsonStatusBuilder
{
    private const int MaxTextLength = 250;

    public PrtgJsonStatusResponse Build(SnmpSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var products = snapshot.Products;
        var stacks = snapshot.Stacks;
        var services = snapshot.Services;

        var healthyProducts = products.Count(p => p.Status == (int)ProductDeploymentStatus.Running);
        var degradedProducts = products.Count(p => p.Status == (int)ProductDeploymentStatus.PartiallyRunning);
        var failedProducts = products.Count(p => p.Status == (int)ProductDeploymentStatus.Failed);
        var maintenanceProducts = products.Count(p => p.OperationMode == 1);

        var totalStacks = stacks.Count;
        var runningStacks = stacks.Count(s => s.Status == (int)StackDeploymentStatus.Running);
        var failedStacks = stacks.Count(s => s.Status == (int)StackDeploymentStatus.Failed);

        var servicesRunning = services.Count(s => s.Running);
        var servicesNotRunning = services.Count(s => !s.Running);

        // Use the same Int formatting as PRTG examples — integer values without decimals.
        var channels = new List<PrtgChannel>
        {
            new() { Channel = "Products total",            Value = Int(products.Count), Unit = "Count" },
            new() { Channel = "Products healthy",          Value = Int(healthyProducts), Unit = "Count" },
            new() { Channel = "Products degraded",         Value = Int(degradedProducts), Unit = "Count",
                    LimitMaxWarning = 0, LimitMode = 1 },
            new() { Channel = "Products failed",           Value = Int(failedProducts), Unit = "Count",
                    LimitMaxError = 0, LimitMode = 1 },
            new() { Channel = "Products in maintenance",   Value = Int(maintenanceProducts), Unit = "Count" },
            new() { Channel = "Stacks total",              Value = Int(totalStacks), Unit = "Count" },
            new() { Channel = "Stacks running",            Value = Int(runningStacks), Unit = "Count" },
            new() { Channel = "Stacks failed",             Value = Int(failedStacks), Unit = "Count",
                    LimitMaxError = 0, LimitMode = 1 },
            new() { Channel = "Services running",          Value = Int(servicesRunning), Unit = "Count" },
            new() { Channel = "Services not running",      Value = Int(servicesNotRunning), Unit = "Count",
                    LimitMaxError = 0, LimitMode = 1 },
            new() { Channel = "Environments",              Value = Int(snapshot.Environments.Count), Unit = "Count" },
            new() { Channel = "DB health",                 Value = Int(snapshot.System.DbHealthy ? 1 : 0),
                    Unit = "Custom", CustomUnit = "state",
                    ValueLookup = "prtg.standardlookups.yesno.stateyesok" },
            new() { Channel = "Uptime",                    Value = Int(snapshot.System.UptimeHundredthsOfSeconds / 100),
                    Unit = "TimeSeconds" },
        };

        return new PrtgJsonStatusResponse
        {
            Prtg = new PrtgResult
            {
                Result = channels,
                Text = BuildSummaryText(snapshot),
            },
        };
    }

    /// <summary>
    /// One-line human summary PRTG shows next to the sensor name. Lists the
    /// most-actionable items first (failed and maintenance) so an operator
    /// sees the problem before the noise.
    /// </summary>
    private static string BuildSummaryText(SnmpSnapshot snapshot)
    {
        var failed = snapshot.Products
            .Where(p => p.Status == (int)ProductDeploymentStatus.Failed)
            .Select(p => $"{p.Name} FAILED")
            .ToList();
        var degraded = snapshot.Products
            .Where(p => p.Status == (int)ProductDeploymentStatus.PartiallyRunning)
            .Select(p => $"{p.Name} degraded")
            .ToList();
        var maintenance = snapshot.Products
            .Where(p => p.OperationMode == 1)
            .Select(p => $"{p.Name} in maintenance")
            .ToList();

        var parts = failed.Concat(degraded).Concat(maintenance).ToList();
        if (parts.Count == 0)
        {
            return snapshot.Products.Count == 0
                ? "No active product deployments"
                : $"{snapshot.Products.Count} product deployment(s) all healthy";
        }

        var joined = string.Join(" · ", parts);
        return joined.Length <= MaxTextLength
            ? joined
            : joined[..(MaxTextLength - 1)] + "…";
    }

    private static string Int(long value) => value.ToString(CultureInfo.InvariantCulture);
}
