using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Precheck;

namespace ReadyStackGo.Application.UseCases.Deployments.Precheck.Rules;

/// <summary>
/// Detects existing deployments for the same stack name and reports their status.
/// </summary>
public class ExistingDeploymentRule : IDeploymentPrecheckRule
{
    public Task<IReadOnlyList<PrecheckItem>> ExecuteAsync(PrecheckContext context, CancellationToken cancellationToken)
    {
        var items = new List<PrecheckItem>();

        if (context.ExistingDeployment == null)
        {
            items.Add(new PrecheckItem(
                "ExistingDeployment",
                PrecheckSeverity.OK,
                "No existing deployment",
                "This is a fresh installation"));
            return Task.FromResult<IReadOnlyList<PrecheckItem>>(items);
        }

        var deployment = context.ExistingDeployment;
        var status = deployment.Status;

        switch (status)
        {
            case DeploymentStatus.Running:
                items.Add(new PrecheckItem(
                    "ExistingDeployment",
                    PrecheckSeverity.Warning,
                    $"Existing deployment is running (upgrade)",
                    $"Stack '{context.StackName}' is currently running. This will be an upgrade — existing containers will be replaced."));
                break;

            case DeploymentStatus.Installing:
            case DeploymentStatus.Upgrading:
                items.Add(new PrecheckItem(
                    "ExistingDeployment",
                    PrecheckSeverity.Error,
                    $"Deployment already in progress ({status})",
                    $"Stack '{context.StackName}' has an active {status.ToString().ToLowerInvariant()} operation. Wait for it to complete or resolve it first."));
                break;

            case DeploymentStatus.Failed:
                items.Add(new PrecheckItem(
                    "ExistingDeployment",
                    PrecheckSeverity.Warning,
                    "Previous deployment failed (retry)",
                    $"Stack '{context.StackName}' previously failed. This deployment will retry."));
                break;

            case DeploymentStatus.Removed:
                items.Add(new PrecheckItem(
                    "ExistingDeployment",
                    PrecheckSeverity.OK,
                    "Previous deployment was removed",
                    "This is a fresh installation after a previous removal."));
                break;
        }

        return Task.FromResult<IReadOnlyList<PrecheckItem>>(items);
    }
}
