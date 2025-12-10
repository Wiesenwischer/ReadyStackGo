namespace ReadyStackGo.Application.UseCases.Deployments.DeployStack;

/// <summary>
/// Response from deploying a stack.
/// </summary>
public class DeployStackResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? DeploymentId { get; set; }
    public string? StackName { get; set; }
    public string? StackVersion { get; set; }
    public List<DeployedServiceInfo> Services { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Session ID used for real-time progress tracking via SignalR.
    /// Clients can subscribe to deployment:{DeploymentSessionId} group.
    /// </summary>
    public string? DeploymentSessionId { get; set; }

    public static DeployStackResponse Failed(string message, params string[] errors) =>
        new()
        {
            Success = false,
            Message = message,
            Errors = errors.ToList()
        };
}
