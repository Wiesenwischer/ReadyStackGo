using System.Text.Json.Serialization;

namespace ReadyStackGo.Domain.Organizations;

/// <summary>
/// Abstract base class for environment configurations.
/// Environments represent isolated deployment targets with their own Docker hosts.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DockerSocketEnvironment), "docker-socket")]
public abstract class Environment
{
    /// <summary>
    /// Unique identifier for this environment (e.g., "production", "test", "development")
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Display name for this environment (e.g., "Production", "Test Environment")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Whether this is the default environment for the organization
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// When this environment was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Returns a unique connection string for this environment.
    /// Used to enforce that each Docker host is only assigned to one environment.
    /// </summary>
    public abstract string GetConnectionString();

    /// <summary>
    /// Returns the environment type identifier (e.g., "docker-socket", "docker-api")
    /// </summary>
    public abstract string GetEnvironmentType();
}
