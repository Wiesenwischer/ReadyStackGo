using MediatR;
using ReadyStackGo.Application.UseCases.Stacks.ListStacks;

namespace ReadyStackGo.Application.UseCases.Stacks.GetStack;

public record GetStackQuery(string StackId) : IRequest<GetStackResult?>;

public record GetStackResult(
    string Id,
    string SourceId,
    string SourceName,
    string Name,
    string? Description,
    List<ServiceItem> Services,
    List<StackVariableItem> Variables,
    List<VolumeItem> Volumes,
    List<NetworkItem> Networks,
    string? FilePath,
    DateTime LastSyncedAt,
    string? Version,
    /// <summary>
    /// Product ID for navigation back to catalog (format: sourceId:productName).
    /// </summary>
    string ProductId
);

/// <summary>
/// Represents a service in a stack.
/// </summary>
public record ServiceItem(
    string Name,
    string Image,
    string? ContainerName,
    List<string> Ports,
    List<string> Volumes,
    Dictionary<string, string> Environment,
    List<string> Networks,
    List<string> DependsOn,
    string? RestartPolicy,
    string? Command,
    ServiceHealthCheckItem? HealthCheck
);

/// <summary>
/// Health check configuration for a service.
/// </summary>
public record ServiceHealthCheckItem(
    List<string> Test,
    string? Interval,
    string? Timeout,
    int? Retries,
    string? StartPeriod
);

/// <summary>
/// Named volume in a stack.
/// </summary>
public record VolumeItem(
    string Name,
    string? Driver,
    bool External
);

/// <summary>
/// Network in a stack.
/// </summary>
public record NetworkItem(
    string Name,
    string Driver,
    bool External
);
