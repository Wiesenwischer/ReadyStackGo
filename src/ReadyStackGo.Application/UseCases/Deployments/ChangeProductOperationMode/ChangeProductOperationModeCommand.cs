namespace ReadyStackGo.Application.UseCases.Deployments.ChangeProductOperationMode;

using MediatR;

/// <summary>
/// Command to change the operation mode of a product deployment.
/// Maintenance mode stops containers of all child stacks.
/// </summary>
public record ChangeProductOperationModeCommand(
    string EnvironmentId,
    string ProductDeploymentId,
    string NewMode,
    string? Reason = null,
    string Source = "Manual") : IRequest<ChangeProductOperationModeResponse>;

/// <summary>
/// Response from changing product operation mode.
/// </summary>
public record ChangeProductOperationModeResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? ProductDeploymentId { get; init; }
    public string? PreviousMode { get; init; }
    public string? NewMode { get; init; }
    public string? TriggerSource { get; init; }

    /// <summary>
    /// Non-fatal error from propagating the state to the product via the maintenance setter
    /// (SQL write / webhook). Null when no setter is configured or it succeeded.
    /// </summary>
    public string? SetterError { get; init; }

    public static ChangeProductOperationModeResponse Ok(
        string productDeploymentId, string previousMode, string newMode,
        string? triggerSource = null, string? setterError = null) =>
        new()
        {
            Success = true,
            ProductDeploymentId = productDeploymentId,
            PreviousMode = previousMode,
            NewMode = newMode,
            TriggerSource = triggerSource,
            SetterError = setterError,
            Message = $"Operation mode changed from {previousMode} to {newMode}"
        };

    public static ChangeProductOperationModeResponse Fail(string message) =>
        new() { Success = false, Message = message };
}
