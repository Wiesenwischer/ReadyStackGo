using ReadyStackGo.Application.UseCases.Containers;

namespace ReadyStackGo.Application.Services.Impl;

/// <summary>
/// Shared helper for resolving container ports.
/// Used by health check strategies to determine the port to check.
/// </summary>
internal static class ContainerPortHelper
{
    /// <summary>
    /// Gets the first exposed port from a container.
    /// </summary>
    public static int? GetFirstExposedPort(ContainerDto container)
    {
        var firstPort = container.Ports?.FirstOrDefault();
        if (firstPort != null && firstPort.PrivatePort > 0)
        {
            return firstPort.PrivatePort;
        }
        return null;
    }
}
