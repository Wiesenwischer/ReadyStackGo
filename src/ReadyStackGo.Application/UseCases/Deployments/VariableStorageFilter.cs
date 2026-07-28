namespace ReadyStackGo.Application.UseCases.Deployments;

/// <summary>
/// Applies the per-variable "save value" opt-out from the deploy/upgrade form.
///
/// An excluded variable is still passed to Docker — the container needs the value to run — but it is
/// not written to the deployment entity. The user asked for it not to be kept, which for a password
/// is the whole point of the toggle.
/// </summary>
public static class VariableStorageFilter
{
    /// <summary>
    /// Returns the variables that may be persisted, dropping every name in
    /// <paramref name="excludeFromStorage"/>.
    /// </summary>
    public static Dictionary<string, string> ForStorage(
        IReadOnlyDictionary<string, string> variables,
        IReadOnlySet<string>? excludeFromStorage)
    {
        if (excludeFromStorage is not { Count: > 0 })
        {
            return new Dictionary<string, string>(variables);
        }

        return variables
            .Where(kvp => !excludeFromStorage.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}
