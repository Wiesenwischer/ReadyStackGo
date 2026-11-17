namespace ReadyStackGo.Domain.Deployment.Observers;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>Type of maintenance setter (mirror of <see cref="ObserverType"/> for writing).</summary>
public enum SetterType
{
    SqlExtendedProperty,
    Webhook
}

/// <summary>The maintenance state RSGO propagates to the product.</summary>
public enum MaintenanceState
{
    Maintenance,
    Normal
}

/// <summary>
/// Configuration for a maintenance setter — the write-side mirror of
/// <see cref="MaintenanceObserverConfig"/>.
/// </summary>
public sealed class MaintenanceSetterConfig : ValueObject
{
    public SetterType Type { get; }

    /// <summary>Delay between firing the setter and stopping containers (0 = none).</summary>
    public TimeSpan GracePeriod { get; }

    /// <summary>Value written for the maintenance state (sqlExtendedProperty).</summary>
    public string MaintenanceValue { get; }

    /// <summary>Value written for the normal state (sqlExtendedProperty).</summary>
    public string NormalValue { get; }

    public ISetterSettings Settings { get; }

    private MaintenanceSetterConfig(
        SetterType type, TimeSpan gracePeriod, string maintenanceValue, string normalValue, ISetterSettings settings)
    {
        Type = type;
        GracePeriod = gracePeriod;
        MaintenanceValue = maintenanceValue;
        NormalValue = normalValue;
        Settings = settings;
    }

    public static MaintenanceSetterConfig Create(
        SetterType type, TimeSpan gracePeriod, string maintenanceValue, string normalValue, ISetterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (gracePeriod < TimeSpan.Zero)
            throw new ArgumentException("Grace period cannot be negative", nameof(gracePeriod));

        return new MaintenanceSetterConfig(type, gracePeriod, maintenanceValue ?? string.Empty, normalValue ?? string.Empty, settings);
    }

    /// <summary>The value to write for the given state (used by the SQL setter).</summary>
    public string ValueForState(MaintenanceState state) =>
        state == MaintenanceState.Maintenance ? MaintenanceValue : NormalValue;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return GracePeriod;
        yield return MaintenanceValue;
        yield return NormalValue;
        yield return Settings;
    }
}

/// <summary>Marker interface for type-specific setter settings.</summary>
public interface ISetterSettings
{
    IEnumerable<string> Validate();
}

/// <summary>Settings for the SQL extended-property setter.</summary>
public sealed class SqlSetterSettings : ValueObject, ISetterSettings
{
    public string ConnectionString { get; }
    public string PropertyName { get; }

    public SqlSetterSettings(string connectionString, string propertyName)
    {
        ConnectionString = connectionString;
        PropertyName = propertyName;
    }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            yield return "connectionString must be specified";
        if (string.IsNullOrWhiteSpace(PropertyName))
            yield return "propertyName must be specified";
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ConnectionString;
        yield return PropertyName;
    }
}

/// <summary>Settings for the signed webhook setter.</summary>
public sealed class WebhookSetterSettings : ValueObject, ISetterSettings
{
    public string Url { get; }

    /// <summary>HMAC-SHA256 secret used to sign the request body. Never logged.</summary>
    public string? Secret { get; }

    public TimeSpan Timeout { get; }
    public int MaxRetries { get; }

    public WebhookSetterSettings(string url, string? secret, TimeSpan timeout, int maxRetries)
    {
        Url = url;
        Secret = secret;
        Timeout = timeout;
        MaxRetries = maxRetries < 0 ? 0 : maxRetries;
    }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
            yield return "url must be specified";
        else if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            yield return "url must be a valid HTTP or HTTPS URL";

        if (Timeout <= TimeSpan.Zero)
            yield return "timeout must be positive";
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Url;
        yield return Secret;
        yield return Timeout;
        yield return MaxRetries;
    }
}

/// <summary>Outcome of a setter invocation (best-effort; failures are non-fatal).</summary>
public sealed record SetterResult(bool Success, bool Skipped, string? Error)
{
    public static SetterResult Ok() => new(true, false, null);
    public static SetterResult WasSkipped(string reason) => new(true, true, reason);
    public static SetterResult Failed(string error) => new(false, false, error);
}
