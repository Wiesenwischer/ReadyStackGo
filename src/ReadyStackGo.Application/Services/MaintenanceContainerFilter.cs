using ReadyStackGo.Application.UseCases.Containers;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Decides which containers a maintenance transition has to touch.
///
/// The selection used to be "state == running", which silently let every other live Docker state
/// through: a container under <c>restart: always</c> that is cycling reports <c>restarting</c>, a
/// suspended one reports <c>paused</c>, and one that was created but never started reports
/// <c>created</c>. None of them were stopped, so they kept running through the maintenance window —
/// connection pools on the product database included, which is exactly what maintenance mode is
/// supposed to release.
///
/// The predicate is therefore inverted: everything that is not already gone is stopped. Only states
/// that cannot hold resources any more (<c>exited</c>, <c>dead</c>) or are already being torn down
/// (<c>removing</c>) are left alone.
/// </summary>
public static class MaintenanceContainerFilter
{
    /// <summary>Label a product uses to opt a container out of maintenance handling.</summary>
    public const string MaintenanceLabel = "rsgo.maintenance";

    /// <summary>Label carrying the deployment stack name a container belongs to.</summary>
    public const string StackLabel = "rsgo.stack";

    /// <summary>Label value that exempts a container from being stopped or started.</summary>
    public const string IgnoreValue = "ignore";

    /// <summary>
    /// True when the container opted out of maintenance handling via
    /// <c>rsgo.maintenance=ignore</c> (the edge proxy and product-contributed maintenance
    /// containers rely on this to outlive the window).
    /// </summary>
    public static bool IsExempt(ContainerDto container)
        => container.Labels.TryGetValue(MaintenanceLabel, out var mode)
           && mode.Equals(IgnoreValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the container carries the given deployment stack name.
    /// </summary>
    public static bool BelongsToStack(ContainerDto container, string stackName)
        => container.Labels.TryGetValue(StackLabel, out var stack) && stack == stackName;

    /// <summary>
    /// True when the container can no longer hold resources, i.e. the stop already took effect.
    /// </summary>
    public static bool IsStopped(ContainerDto container)
        => IsState(container, "exited") || IsState(container, "dead");

    /// <summary>
    /// True when a maintenance transition must stop this container: it is not exempt, not already
    /// stopped, and not in the middle of being removed.
    /// </summary>
    public static bool ShouldStop(ContainerDto container)
        => !IsExempt(container) && !IsStopped(container) && !IsState(container, "removing");

    /// <summary>
    /// True when exiting maintenance must start this container again.
    /// </summary>
    public static bool ShouldStart(ContainerDto container)
        => !IsExempt(container) && (IsState(container, "exited") || IsState(container, "created"));

    private static bool IsState(ContainerDto container, string state)
        => container.State.Equals(state, StringComparison.OrdinalIgnoreCase);
}
