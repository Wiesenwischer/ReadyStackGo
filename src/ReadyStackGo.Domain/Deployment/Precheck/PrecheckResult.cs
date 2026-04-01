namespace ReadyStackGo.Domain.Deployment.Precheck;

/// <summary>
/// Aggregated result of all deployment precheck rules.
/// </summary>
public record PrecheckResult(IReadOnlyList<PrecheckItem> Checks)
{
    /// <summary>Whether any check produced an error.</summary>
    public bool HasErrors => Checks.Any(c => c.Severity == PrecheckSeverity.Error);

    /// <summary>Whether any check produced a warning.</summary>
    public bool HasWarnings => Checks.Any(c => c.Severity == PrecheckSeverity.Warning);

    /// <summary>Whether deployment can proceed (no errors).</summary>
    public bool CanDeploy => !HasErrors;

    /// <summary>Human-readable summary of the precheck outcome.</summary>
    public string Summary => HasErrors
        ? $"{ErrorCount} error(s) found — deployment blocked"
        : HasWarnings
            ? $"All checks passed with {WarningCount} warning(s)"
            : "All checks passed";

    /// <summary>Number of checks that produced errors.</summary>
    public int ErrorCount => Checks.Count(c => c.Severity == PrecheckSeverity.Error);

    /// <summary>Number of checks that produced warnings.</summary>
    public int WarningCount => Checks.Count(c => c.Severity == PrecheckSeverity.Warning);

    /// <summary>Gets all checks for a specific rule.</summary>
    public IEnumerable<PrecheckItem> GetByRule(string rule) =>
        Checks.Where(c => c.Rule == rule);

    /// <summary>Gets all checks with a specific severity.</summary>
    public IEnumerable<PrecheckItem> GetBySeverity(PrecheckSeverity severity) =>
        Checks.Where(c => c.Severity == severity);

    /// <summary>Creates an empty result with no checks.</summary>
    public static PrecheckResult Empty => new([]);
}
