using ReadyStackGo.Application.UseCases.Deployments.Precheck;
using ReadyStackGo.Domain.Deployment.Precheck;

namespace ReadyStackGo.Infrastructure.Services.Deployment.Precheck;

/// <summary>
/// Checks for host port conflicts between the stack's services and already running containers.
/// </summary>
public class PortConflictRule : IDeploymentPrecheckRule
{
    public Task<IReadOnlyList<PrecheckItem>> ExecuteAsync(PrecheckContext context, CancellationToken cancellationToken)
    {
        var items = new List<PrecheckItem>();

        // Collect all host ports currently in use (excluding containers belonging to this stack)
        var ownContainerPrefix = $"{context.StackName}-";
        var usedPorts = new Dictionary<int, string>(); // port → container name

        foreach (var container in context.RunningContainers)
        {
            // Skip containers belonging to this stack (upgrade scenario)
            if (container.Name.StartsWith(ownContainerPrefix, StringComparison.OrdinalIgnoreCase) ||
                container.Name.Equals(context.StackName, StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip non-running containers
            if (!container.State.Equals("running", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var port in container.Ports)
            {
                if (port.PublicPort > 0)
                {
                    usedPorts.TryAdd(port.PublicPort, container.Name);
                }
            }
        }

        // Check each service's port mappings for conflicts
        var hasConflicts = false;
        foreach (var service in context.StackDefinition.Services)
        {
            foreach (var portMapping in service.Ports)
            {
                if (string.IsNullOrEmpty(portMapping.HostPort) || portMapping.HostPort == "0")
                    continue; // Random port assignment — no conflict possible

                // Handle port ranges (e.g., "8080-8090")
                var hostPorts = ExpandPortRange(portMapping.HostPort);

                foreach (var hostPort in hostPorts)
                {
                    if (usedPorts.TryGetValue(hostPort, out var conflictingContainer))
                    {
                        hasConflicts = true;
                        items.Add(new PrecheckItem(
                            "PortConflict",
                            PrecheckSeverity.Error,
                            $"Port {hostPort} already in use",
                            $"Host port {hostPort}/{portMapping.Protocol} is used by container '{conflictingContainer}'",
                            service.Name));
                    }
                }
            }
        }

        if (!hasConflicts)
        {
            items.Add(new PrecheckItem(
                "PortConflict",
                PrecheckSeverity.OK,
                "No port conflicts",
                "All required host ports are available"));
        }

        return Task.FromResult<IReadOnlyList<PrecheckItem>>(items);
    }

    internal static IEnumerable<int> ExpandPortRange(string hostPort)
    {
        if (hostPort.Contains('-'))
        {
            var parts = hostPort.Split('-');
            if (int.TryParse(parts[0], out var start) && int.TryParse(parts[1], out var end))
            {
                for (var port = start; port <= end; port++)
                    yield return port;
                yield break;
            }
        }

        if (int.TryParse(hostPort, out var singlePort))
            yield return singlePort;
    }
}
