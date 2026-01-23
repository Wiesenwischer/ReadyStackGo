namespace ReadyStackGo.Domain.Deployment.Environments;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Represents a persisted variable value for an environment.
/// Used to auto-fill deployment variables based on previous deployments.
/// </summary>
public class EnvironmentVariable : Entity<EnvironmentVariableId>
{
    public EnvironmentId EnvironmentId { get; private set; } = null!;
    public string Key { get; private set; } = null!;
    public string Value { get; private set; } = null!;
    public bool IsEncrypted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // For EF Core
    protected EnvironmentVariable() { }

    private EnvironmentVariable(
        EnvironmentVariableId id,
        EnvironmentId environmentId,
        string key,
        string value,
        bool isEncrypted)
    {
        SelfAssertArgumentNotNull(id, "EnvironmentVariableId is required.");
        SelfAssertArgumentNotNull(environmentId, "EnvironmentId is required.");
        SelfAssertArgumentNotEmpty(key, "Variable key is required.");
        SelfAssertArgumentLength(key, 1, 500, "Variable key must be 500 characters or less.");
        SelfAssertArgumentNotNull(value, "Variable value cannot be null.");

        Id = id;
        EnvironmentId = environmentId;
        Key = key;
        Value = value;
        IsEncrypted = isEncrypted;
        CreatedAt = SystemClock.UtcNow;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Creates a new environment variable.
    /// </summary>
    public static EnvironmentVariable Create(
        EnvironmentVariableId id,
        EnvironmentId environmentId,
        string key,
        string value,
        bool isEncrypted = false)
    {
        return new EnvironmentVariable(id, environmentId, key, value, isEncrypted);
    }

    /// <summary>
    /// Updates the variable value.
    /// </summary>
    public void UpdateValue(string value, bool isEncrypted = false)
    {
        SelfAssertArgumentNotNull(value, "Variable value cannot be null.");

        Value = value;
        IsEncrypted = isEncrypted;
        UpdatedAt = SystemClock.UtcNow;
    }

    public override string ToString() =>
        $"EnvironmentVariable [id={Id}, environmentId={EnvironmentId}, key={Key}, isEncrypted={IsEncrypted}]";
}
