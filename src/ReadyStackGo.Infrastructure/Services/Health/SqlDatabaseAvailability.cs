namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>Whether a product database can currently be read by RSGO.</summary>
public enum SqlDatabaseAvailabilityKind
{
    /// <summary>ONLINE and MULTI_USER — safe to read.</summary>
    Available,

    /// <summary>
    /// Held exclusively or not online (SINGLE_USER, RESTORING, OFFLINE, …). The product is
    /// undergoing maintenance and RSGO must not connect to the database.
    /// </summary>
    Exclusive,

    /// <summary>
    /// The availability could not be determined (no permission on <c>master</c>, database not
    /// visible, instance unreachable). Callers fall back to their normal check.
    /// </summary>
    Unknown
}

/// <summary>Result of an availability probe, with a detail string for logs and the UI.</summary>
public sealed record SqlDatabaseAvailability(SqlDatabaseAvailabilityKind Kind, string Detail)
{
    public static SqlDatabaseAvailability Available(string detail) => new(SqlDatabaseAvailabilityKind.Available, detail);

    public static SqlDatabaseAvailability Exclusive(string detail) => new(SqlDatabaseAvailabilityKind.Exclusive, detail);

    public static SqlDatabaseAvailability Unknown(string detail) => new(SqlDatabaseAvailabilityKind.Unknown, detail);

    public bool IsExclusive => Kind == SqlDatabaseAvailabilityKind.Exclusive;

    public bool IsUnknown => Kind == SqlDatabaseAvailabilityKind.Unknown;
}

/// <summary>
/// Reads a database's availability from server metadata instead of connecting to the database.
/// </summary>
public interface ISqlDatabaseAvailabilityProbe
{
    /// <summary>
    /// Probes the database named in <paramref name="observerConnectionString"/> by querying
    /// <c>sys.databases</c> over a <c>master</c> connection derived from the same credentials.
    /// </summary>
    Task<SqlDatabaseAvailability> ProbeAsync(string observerConnectionString, CancellationToken cancellationToken = default);
}
