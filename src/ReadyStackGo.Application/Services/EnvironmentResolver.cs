using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Resolves an environment identifier that can be either a GUID or a name.
/// Used by hook endpoints to allow CI/CD pipelines to reference environments by name.
/// </summary>
public static class EnvironmentResolver
{
    /// <summary>
    /// Resolves an environment ID or name to an EnvironmentId.
    /// If the value is a valid GUID, it is used directly.
    /// Otherwise, a case-insensitive name lookup is performed.
    /// </summary>
    /// <returns>The resolved EnvironmentId, or an error message.</returns>
    public static (EnvironmentId? Id, string? Error) Resolve(
        string? idOrName,
        IEnvironmentRepository environmentRepository)
    {
        if (string.IsNullOrWhiteSpace(idOrName))
        {
            return (null, "EnvironmentId is required. Provide a GUID or environment name.");
        }

        // Try as GUID first
        if (Guid.TryParse(idOrName, out var envGuid))
        {
            return (new EnvironmentId(envGuid), null);
        }

        // Fall back to case-insensitive name lookup
        var match = environmentRepository.FindByName(idOrName);
        if (match == null)
        {
            return (null, $"Environment '{idOrName}' not found. Provide a valid GUID or environment name.");
        }

        return (match.Id, null);
    }
}
