namespace ReadyStackGo.Application.UseCases.Health;

/// <summary>
/// DTO for detailed health information of a stack.
/// </summary>
public class StackHealthDto
{
    public required string DeploymentId { get; init; }
    public required string EnvironmentId { get; init; }
    public required string StackName { get; init; }
    public required string? CurrentVersion { get; init; }
    public required string? TargetVersion { get; init; }

    // Overall status (UI presentation like colors/icons handled in frontend)
    public required string OverallStatus { get; init; }

    // Operation mode
    public required string OperationMode { get; init; }

    // Summary
    public required string StatusMessage { get; init; }
    public required bool RequiresAttention { get; init; }
    public required DateTime CapturedAtUtc { get; init; }

    // Self health (services/containers)
    public required SelfHealthDto Self { get; init; }

    // Optional: Bus health
    public BusHealthDto? Bus { get; init; }

    // Optional: Infra health
    public InfraHealthDto? Infra { get; init; }
}

/// <summary>
/// DTO for self health (container/service status).
/// </summary>
public class SelfHealthDto
{
    public required string Status { get; init; }
    public required int HealthyCount { get; init; }
    public required int TotalCount { get; init; }
    public required List<ServiceHealthDto> Services { get; init; }
}

/// <summary>
/// DTO for individual service health.
/// </summary>
public class ServiceHealthDto
{
    public required string Name { get; init; }
    public required string Status { get; init; }
    public string? ContainerId { get; init; }
    public string? ContainerName { get; init; }
    public string? Reason { get; init; }

    /// <summary>
    /// Number of container restarts. Null means not loaded (healthy containers).
    /// </summary>
    public int? RestartCount { get; init; }
}

/// <summary>
/// DTO for bus health (NServiceBus).
/// </summary>
public class BusHealthDto
{
    public required string Status { get; init; }
    public string? TransportKey { get; init; }
    public bool HasCriticalError { get; init; }
    public string? CriticalErrorMessage { get; init; }
    public DateTime? LastHealthPingProcessedUtc { get; init; }
    public List<BusEndpointHealthDto> Endpoints { get; init; } = new();
}

/// <summary>
/// DTO for bus endpoint health.
/// </summary>
public class BusEndpointHealthDto
{
    public required string EndpointName { get; init; }
    public required string Status { get; init; }
    public DateTime? LastPingUtc { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// DTO for infrastructure health.
/// </summary>
public class InfraHealthDto
{
    public required string Status { get; init; }
    public List<DatabaseHealthDto> Databases { get; init; } = new();
    public List<DiskHealthDto> Disks { get; init; } = new();
    public List<ExternalServiceHealthDto> ExternalServices { get; init; } = new();
}

/// <summary>
/// DTO for database health.
/// </summary>
public class DatabaseHealthDto
{
    public required string Id { get; init; }
    public required string Status { get; init; }
    public int? LatencyMs { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// DTO for disk health.
/// </summary>
public class DiskHealthDto
{
    public required string Mount { get; init; }
    public required string Status { get; init; }
    public double? FreePercent { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// DTO for external service health.
/// </summary>
public class ExternalServiceHealthDto
{
    public required string Id { get; init; }
    public required string Status { get; init; }
    public string? Error { get; init; }
    public int? ResponseTimeMs { get; init; }
}

/// <summary>
/// DTO for environment health summary (list view).
/// </summary>
public class EnvironmentHealthSummaryDto
{
    public required string EnvironmentId { get; init; }
    public required string EnvironmentName { get; init; }
    public required int TotalStacks { get; init; }
    public required int HealthyCount { get; init; }
    public required int DegradedCount { get; init; }
    public required int UnhealthyCount { get; init; }
    public required List<StackHealthSummaryDto> Stacks { get; init; }
}

/// <summary>
/// DTO for stack health in a summary list.
/// </summary>
public class StackHealthSummaryDto
{
    public required string DeploymentId { get; init; }
    public required string StackName { get; init; }
    public required string? CurrentVersion { get; init; }

    // Overall status (UI presentation like colors/icons handled in frontend)
    public required string OverallStatus { get; init; }

    // Operation mode
    public required string OperationMode { get; init; }

    // Services summary
    public required int HealthyServices { get; init; }
    public required int TotalServices { get; init; }

    // Status
    public required string StatusMessage { get; init; }
    public required bool RequiresAttention { get; init; }
    public required DateTime CapturedAtUtc { get; init; }
}

/// <summary>
/// Response for getting stack health.
/// </summary>
public class GetStackHealthResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public StackHealthDto? Data { get; set; }

    public static GetStackHealthResponse Ok(StackHealthDto data) =>
        new() { Success = true, Data = data };

    public static GetStackHealthResponse Failure(string message) =>
        new() { Success = false, Message = message };
}

/// <summary>
/// Response for getting environment health summary.
/// </summary>
public class GetEnvironmentHealthSummaryResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public EnvironmentHealthSummaryDto? Data { get; set; }

    public static GetEnvironmentHealthSummaryResponse Ok(EnvironmentHealthSummaryDto data) =>
        new() { Success = true, Data = data };

    public static GetEnvironmentHealthSummaryResponse Failure(string message) =>
        new() { Success = false, Message = message };
}

/// <summary>
/// Response for getting health history.
/// </summary>
public class GetHealthHistoryResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<StackHealthSummaryDto> History { get; set; } = new();

    public static GetHealthHistoryResponse Ok(List<StackHealthSummaryDto> history) =>
        new() { Success = true, History = history };

    public static GetHealthHistoryResponse Failure(string message) =>
        new() { Success = false, Message = message };
}
