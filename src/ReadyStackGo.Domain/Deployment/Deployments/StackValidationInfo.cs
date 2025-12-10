namespace ReadyStackGo.Domain.Deployment.Deployments;

/// <summary>
/// Value object representing stack information needed for deployment validation.
/// This is a Deployment subdomain concept - the Application Layer maps from
/// StackManagement.StackDefinition to this class to maintain domain boundaries.
/// </summary>
public class StackValidationInfo
{
    /// <summary>
    /// Stack identifier (sourceId:stackName format).
    /// </summary>
    public required string StackId { get; init; }

    /// <summary>
    /// List of required variable names.
    /// </summary>
    public IReadOnlyList<RequiredVariableInfo> RequiredVariables { get; init; } = [];

    /// <summary>
    /// List of all variables with validation rules.
    /// </summary>
    public IReadOnlyList<VariableValidationInfo> Variables { get; init; } = [];

    /// <summary>
    /// List of service names in the stack.
    /// </summary>
    public IReadOnlyList<string> ServiceNames { get; init; } = [];

    /// <summary>
    /// Whether the stack has any services.
    /// </summary>
    public bool HasServices => ServiceNames.Count > 0;
}

/// <summary>
/// Information about a required variable.
/// </summary>
public record RequiredVariableInfo(string Name, string? Label);

/// <summary>
/// Value object representing variable validation rules within the Deployment subdomain.
/// The Application Layer creates this from StackManagement.StackVariable.
/// </summary>
public record VariableValidationInfo
{
    /// <summary>
    /// Variable name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable label.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Whether this variable is required.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Validation delegate that checks if a value is valid.
    /// Returns a list of error messages, empty if valid.
    /// </summary>
    public Func<string?, IReadOnlyList<string>> Validate { get; init; } = _ => [];
}
