namespace ReadyStackGo.Application.UseCases.Deployments.ChangeOperationMode;

using MediatR;

/// <summary>
/// Command to change the operation mode of a deployment.
/// </summary>
public record ChangeOperationModeCommand(
    string DeploymentId,
    string NewMode,
    string? Reason = null,
    string? TargetVersion = null) : IRequest<ChangeOperationModeResponse>;

/// <summary>
/// Response from changing operation mode.
/// </summary>
public record ChangeOperationModeResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? DeploymentId { get; init; }
    public string? PreviousMode { get; init; }
    public string? NewMode { get; init; }

    public static ChangeOperationModeResponse Ok(string deploymentId, string previousMode, string newMode) =>
        new()
        {
            Success = true,
            DeploymentId = deploymentId,
            PreviousMode = previousMode,
            NewMode = newMode,
            Message = $"Operation mode changed from {previousMode} to {newMode}"
        };

    public static ChangeOperationModeResponse Fail(string message) =>
        new() { Success = false, Message = message };
}
