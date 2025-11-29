namespace ReadyStackGo.Domain.Organizations;

/// <summary>
/// Aggregate root representing an organization with its environments.
/// An organization can exist without environments after initial wizard setup.
/// </summary>
public class Organization
{
    /// <summary>
    /// Unique identifier for the organization (e.g., "acme-corp")
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Display name of the organization (e.g., "Acme Corporation")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Collection of environments belonging to this organization.
    /// Empty after wizard completion; environments are added via Settings UI.
    /// </summary>
    public List<Environment> Environments { get; set; } = new();

    /// <summary>
    /// When this organization was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this organization was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a new organization without any environments.
    /// </summary>
    public static Organization Create(string id, string name)
    {
        return new Organization
        {
            Id = id,
            Name = name,
            Environments = new List<Environment>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Adds an environment to this organization.
    /// Validates that the connection string is unique across all environments.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when connection string is already in use</exception>
    public void AddEnvironment(Environment environment)
    {
        var connectionString = environment.GetConnectionString();

        if (Environments.Any(e => e.GetConnectionString() == connectionString))
        {
            throw new InvalidOperationException(
                $"An environment with connection '{connectionString}' already exists. " +
                "Each Docker host can only be assigned to one environment.");
        }

        if (Environments.Any(e => e.Id == environment.Id))
        {
            throw new InvalidOperationException(
                $"An environment with ID '{environment.Id}' already exists.");
        }

        // If this is the first environment, make it default
        if (Environments.Count == 0)
        {
            environment.IsDefault = true;
        }

        Environments.Add(environment);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Removes an environment from this organization.
    /// If the removed environment was the default, the first remaining environment becomes the new default.
    /// </summary>
    public void RemoveEnvironment(string environmentId)
    {
        var environment = Environments.FirstOrDefault(e => e.Id == environmentId);

        if (environment == null)
        {
            throw new InvalidOperationException($"Environment '{environmentId}' not found.");
        }

        var wasDefault = environment.IsDefault;
        Environments.Remove(environment);

        // If we removed the default and other environments exist, set the first one as default
        if (wasDefault && Environments.Count > 0)
        {
            Environments[0].IsDefault = true;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets an environment as the default for this organization.
    /// </summary>
    public void SetDefaultEnvironment(string environmentId)
    {
        var environment = Environments.FirstOrDefault(e => e.Id == environmentId)
            ?? throw new InvalidOperationException($"Environment '{environmentId}' not found.");

        foreach (var env in Environments)
        {
            env.IsDefault = env.Id == environmentId;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the default environment, or null if no environments exist.
    /// </summary>
    public Environment? GetDefaultEnvironment()
    {
        return Environments.FirstOrDefault(e => e.IsDefault)
            ?? Environments.FirstOrDefault();
    }

    /// <summary>
    /// Gets an environment by its ID.
    /// </summary>
    public Environment? GetEnvironment(string environmentId)
    {
        return Environments.FirstOrDefault(e => e.Id == environmentId);
    }
}
