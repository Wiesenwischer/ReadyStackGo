using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Applies a maintenance setter (writes the SQL flag / calls the webhook) for an RSGO-initiated
/// maintenance transition. Best-effort: never throws; returns the outcome so the caller can
/// surface it without aborting the transition.
/// </summary>
public interface IMaintenanceSetterService
{
    Task<SetterResult> ApplyAsync(
        MaintenanceSetterConfig? config,
        MaintenanceState state,
        CancellationToken cancellationToken = default);
}
