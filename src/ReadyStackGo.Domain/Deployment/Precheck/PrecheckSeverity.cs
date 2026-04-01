namespace ReadyStackGo.Domain.Deployment.Precheck;

/// <summary>
/// Severity level for a deployment precheck item.
/// </summary>
public enum PrecheckSeverity
{
    /// <summary>Check passed — no issues found.</summary>
    OK = 0,

    /// <summary>Potential issue — deployment can proceed but user should be aware.</summary>
    Warning = 1,

    /// <summary>Blocking issue — deployment should not proceed.</summary>
    Error = 2
}
