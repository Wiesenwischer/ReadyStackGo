namespace ReadyStackGo.Domain.Deployment.Edge;

using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

/// <summary>
/// Pure projection of RSGO's authoritative deploy state + maintenance flag onto the
/// edge's desired state. No health-guessing: the inputs are
/// <see cref="ProductDeploymentStatus"/>, the <see cref="OperationMode"/> and the active
/// <see cref="MaintenanceTrigger"/>.
///
/// Rules (locked decision §5):
/// <list type="bullet">
/// <item><c>Running</c> + <c>Normal</c> → <see cref="EdgeMode.Proxy"/> (the only proxy case).</item>
/// <item>Maintenance flag set → maintenance page, planned wording (even if the product is otherwise up).</item>
/// <item><c>Deploying/Redeploying/Upgrading</c> → maintenance page, "deploying" state.</item>
/// <item>Any other state (<c>Failed/Stopped/PartiallyRunning/Removing/...</c>) → maintenance page,
/// "temporarily unavailable" wording.</item>
/// </list>
/// The maintenance flag only changes the <em>wording</em>; whether to proxy is decided
/// purely by the deploy state.
/// </summary>
public static class EdgeStateResolver
{
    private static readonly HashSet<ProductDeploymentStatus> InProgressStatuses = new()
    {
        ProductDeploymentStatus.Deploying,
        ProductDeploymentStatus.Redeploying,
        ProductDeploymentStatus.Upgrading
    };

    public static EdgeDesiredState Resolve(
        ProductDeploymentStatus status,
        OperationMode operationMode,
        MaintenanceTrigger? maintenanceTrigger,
        string? productVersion = null)
    {
        var isMaintenanceFlag = operationMode == OperationMode.Maintenance;

        // 1. The single proxy case: product fully running and not in maintenance.
        if (status == ProductDeploymentStatus.Running && !isMaintenanceFlag)
        {
            return new EdgeDesiredState(
                EdgeMode.Proxy,
                EdgeStatusState.Running,
                PlannedMaintenance: false,
                Reason: null,
                Until: null,
                ProductVersion: productVersion);
        }

        // 2. Operator/observer-declared maintenance → planned-maintenance wording.
        if (isMaintenanceFlag)
        {
            return new EdgeDesiredState(
                EdgeMode.Maintenance,
                EdgeStatusState.Maintenance,
                PlannedMaintenance: true,
                Reason: maintenanceTrigger?.Reason,
                Until: null,
                ProductVersion: productVersion);
        }

        // 3. (Re)deploy/upgrade in progress → "deploying".
        if (InProgressStatuses.Contains(status))
        {
            return new EdgeDesiredState(
                EdgeMode.Maintenance,
                EdgeStatusState.Deploying,
                PlannedMaintenance: false,
                Reason: null,
                Until: null,
                ProductVersion: productVersion);
        }

        // 4. Failed/Stopped/PartiallyRunning/Removing/etc → temporarily unavailable.
        return new EdgeDesiredState(
            EdgeMode.Maintenance,
            EdgeStatusState.Maintenance,
            PlannedMaintenance: false,
            Reason: null,
            Until: null,
            ProductVersion: productVersion);
    }
}
