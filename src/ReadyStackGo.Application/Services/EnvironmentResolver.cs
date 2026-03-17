using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Resolves an environment from separate ID and name fields.
/// Priority: environmentId (GUID) > environmentName (name lookup) > fallback claim.
/// Used by hook endpoints to allow CI/CD pipelines to reference environments by name.
/// </summary>
public static class EnvironmentResolver
{
    /// <summary>
    /// Resolves an environment from separate ID and name fields.
    /// Priority: environmentId > environmentName.
    /// </summary>
    /// <param name="environmentId">Optional GUID identifier.</param>
    /// <param name="environmentName">Optional environment name for case-insensitive lookup.</param>
    /// <param name="environmentRepository">Repository for name-based lookups.</param>
    /// <returns>The resolved EnvironmentId, or an error message.</returns>
    public static (EnvironmentId? Id, string? Error) Resolve(
        string? environmentId,
        string? environmentName,
        IEnvironmentRepository environmentRepository)
    {
        // 1. Try environmentId as GUID
        if (!string.IsNullOrWhiteSpace(environmentId))
        {
            if (Guid.TryParse(environmentId, out var envGuid))
            {
                return (new EnvironmentId(envGuid), null);
            }

            return (null, $"Invalid EnvironmentId format: '{environmentId}'. Must be a valid GUID.");
        }

        // 2. Try environmentName lookup
        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            var match = environmentRepository.FindByName(environmentName);
            if (match == null)
            {
                return (null, $"Environment with name '{environmentName}' not found.");
            }

            return (match.Id, null);
        }

        // 3. Neither provided
        return (null, "Either EnvironmentId (GUID) or EnvironmentName is required.");
    }
}
