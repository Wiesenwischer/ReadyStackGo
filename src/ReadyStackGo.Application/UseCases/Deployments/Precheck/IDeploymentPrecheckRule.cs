using ReadyStackGo.Domain.Deployment.Precheck;

namespace ReadyStackGo.Application.UseCases.Deployments.Precheck;

/// <summary>
/// Interface for a single deployment precheck rule.
/// Each rule inspects one aspect of deployment readiness and returns check items.
/// </summary>
public interface IDeploymentPrecheckRule
{
    /// <summary>
    /// Executes this precheck rule and returns one or more check items.
    /// </summary>
    Task<IReadOnlyList<PrecheckItem>> ExecuteAsync(PrecheckContext context, CancellationToken cancellationToken);
}
