namespace ReadyStackGo.Domain.Deployment.Precheck;

/// <summary>
/// Result of a single stack within a product precheck.
/// </summary>
public record ProductPrecheckStackResult(
    string StackId,
    string StackName,
    PrecheckResult Result);

/// <summary>
/// Aggregated result of a product-level precheck (all stacks).
/// </summary>
public record ProductPrecheckResult(IReadOnlyList<ProductPrecheckStackResult> Stacks)
{
    /// <summary>Whether any stack has errors.</summary>
    public bool HasErrors => Stacks.Any(s => s.Result.HasErrors);

    /// <summary>Whether any stack has warnings.</summary>
    public bool HasWarnings => Stacks.Any(s => s.Result.HasWarnings);

    /// <summary>Whether deployment can proceed (no stack has errors).</summary>
    public bool CanDeploy => Stacks.All(s => s.Result.CanDeploy);

    /// <summary>Human-readable summary of the product precheck outcome.</summary>
    public string Summary
    {
        get
        {
            var errorStacks = Stacks.Count(s => s.Result.HasErrors);
            var warningStacks = Stacks.Count(s => s.Result.HasWarnings && !s.Result.HasErrors);

            if (errorStacks > 0)
                return $"{errorStacks} stack(s) have errors — deployment blocked";
            if (warningStacks > 0)
                return $"All checks passed with warnings in {warningStacks} stack(s)";
            return $"All checks passed for {Stacks.Count} stack(s)";
        }
    }

    /// <summary>Total error count across all stacks.</summary>
    public int TotalErrorCount => Stacks.Sum(s => s.Result.ErrorCount);

    /// <summary>Total warning count across all stacks.</summary>
    public int TotalWarningCount => Stacks.Sum(s => s.Result.WarningCount);

    /// <summary>Creates an empty result with no stacks.</summary>
    public static ProductPrecheckResult Empty => new([]);
}
