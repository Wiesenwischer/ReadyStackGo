namespace ReadyStackGo.Application.UseCases.Deployments.RollbackDeployment;

/// <summary>
/// Response from initiating a deployment rollback.
/// </summary>
public class RollbackDeploymentResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? DeploymentId { get; set; }
    public string? TargetVersion { get; set; }
    public string? PreviousVersion { get; set; }
}
