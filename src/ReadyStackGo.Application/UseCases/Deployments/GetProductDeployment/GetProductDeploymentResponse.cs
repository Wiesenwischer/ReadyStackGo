namespace ReadyStackGo.Application.UseCases.Deployments.GetProductDeployment;

/// <summary>
/// Detailed response for a product deployment.
/// </summary>
public class GetProductDeploymentResponse
{
    public required string ProductDeploymentId { get; set; }
    public required string EnvironmentId { get; set; }
    public required string ProductGroupId { get; set; }
    public required string ProductId { get; set; }
    public required string ProductName { get; set; }
    public required string ProductDisplayName { get; set; }
    public required string ProductVersion { get; set; }
    public required string DeploymentName { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public bool ContinueOnError { get; set; }
    public int TotalStacks { get; set; }
    public int CompletedStacks { get; set; }
    public int FailedStacks { get; set; }
    public string? PreviousVersion { get; set; }
    public int UpgradeCount { get; set; }
    public bool CanRetry { get; set; }
    public bool CanUpgrade { get; set; }
    public bool CanRemove { get; set; }
    public bool CanRedeploy { get; set; }
    public bool CanStop { get; set; }
    public bool CanRestart { get; set; }
    public bool CanEnterMaintenance { get; set; }
    public bool CanExitMaintenance { get; set; }
    public required string OperationMode { get; set; }
    public MaintenanceTriggerDto? MaintenanceTrigger { get; set; }
    public double? DurationSeconds { get; set; }
    public List<ProductStackDeploymentDto> Stacks { get; set; } = new();
    public List<DeploymentVariableDto> SharedVariables { get; set; } = new();

    // PRTG integration (Variant 3) — null when the deployment is not linked.
    public string? PrtgConnectionId { get; set; }
    public int? PrtgDeviceId { get; set; }
    public DateTime? PrtgLastSyncedAt { get; set; }
    // PRTG inline registration (Variant 2) — null when not set. ApiToken is
    // never echoed back; HasInlinePrtgApiToken is a boolean indicator only.
    public string? InlinePrtgUrl { get; set; }
    public bool HasInlinePrtgApiToken { get; set; }
    public int? InlinePrtgTemplateDeviceId { get; set; }
    public bool InlinePrtgVerifyTls { get; set; } = true;
}

/// <summary>
/// DTO for a stack within a product deployment.
/// </summary>
public class ProductStackDeploymentDto
{
    public required string StackName { get; set; }
    public required string StackDisplayName { get; set; }
    public required string StackId { get; set; }
    public string? DeploymentId { get; set; }
    public string? DeploymentStackName { get; set; }
    public required string Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int Order { get; set; }
    public int ServiceCount { get; set; }
    public bool IsNewInUpgrade { get; set; }

    /// <summary>
    /// Variables configured for this stack in the current deployment. Carried
    /// through so the Upgrade form can pre-fill per-stack values (the backend
    /// merges them anyway, but the frontend validates required variables before
    /// sending the request). Secret values are withheld — see <see cref="DeploymentVariableDto"/>.
    /// </summary>
    public List<DeploymentVariableDto> Variables { get; set; } = new();
}

/// <summary>
/// DTO for the maintenance trigger information.
/// </summary>
public class MaintenanceTriggerDto
{
    public required string Source { get; set; }
    public string? Reason { get; set; }
    public DateTime TriggeredAtUtc { get; set; }
    public string? TriggeredBy { get; set; }
}

/// <summary>
/// A variable of a deployment as exposed to clients.
///
/// Secret values never leave the server: for a password or a connection string,
/// <see cref="Value"/> is null and <see cref="HasValue"/> tells the client whether something is
/// stored. That is enough for the upgrade form to satisfy required-variable validation without
/// knowing the value — the backend merges the stored value when it deploys.
/// </summary>
public class DeploymentVariableDto
{
    public required string Name { get; set; }

    /// <summary>The value, or null when <see cref="IsSecret"/> is true.</summary>
    public string? Value { get; set; }

    /// <summary>Whether this variable's value is withheld.</summary>
    public bool IsSecret { get; set; }

    /// <summary>Whether a non-empty value is stored for this variable.</summary>
    public bool HasValue { get; set; }
}
