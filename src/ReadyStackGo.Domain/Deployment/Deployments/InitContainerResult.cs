namespace ReadyStackGo.Domain.Deployment.Deployments;

/// <summary>
/// Value object representing the result of an init container execution.
/// Stored as JSON in the Deployment aggregate.
/// </summary>
public record InitContainerResult(
    string ServiceName,
    bool Success,
    int ExitCode,
    DateTime ExecutedAtUtc,
    string? LogOutput = null);
