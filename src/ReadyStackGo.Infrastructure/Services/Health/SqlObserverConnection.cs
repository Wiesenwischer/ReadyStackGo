using Microsoft.Data.SqlClient;

namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>
/// Connection-string handling shared by the SQL maintenance observers and the SQL setter.
///
/// RSGO reads a product database once per polling interval — a single scalar value. ADO.NET
/// connection pooling keeps the physical session alive after the read, and because the next poll
/// refreshes it, the pool's idle cleanup never reclaims it. Product maintenance workflows that
/// wait for all sessions to close before taking a database exclusively (switching it to
/// SINGLE_USER, restoring it) then wait on RSGO indefinitely — the observer that is supposed to
/// react to the maintenance flag prevents the effect of its own reaction.
///
/// Pooling is therefore forced off for every connection RSGO opens itself: the session exists for
/// the duration of the read and is gone afterwards. The cost is one login per poll, which is
/// irrelevant at these intervals. Product containers are unaffected — RSGO does not build their
/// connection strings, and they need their pools.
/// </summary>
public static class SqlObserverConnection
{
    /// <summary>
    /// Identifies RSGO's own sessions in sys.dm_exec_sessions / sp_who2, so an operator can tell
    /// them apart from the product's connections when diagnosing a blocked maintenance window.
    /// </summary>
    public const string ApplicationName = "ReadyStackGo-Maintenance";

    /// <summary>
    /// Returns the connection string with pooling disabled and an identifying application name.
    /// A connection string RSGO cannot parse is returned unchanged — the caller surfaces the
    /// resulting connect error instead of failing here.
    /// </summary>
    /// <param name="connectionString">The connection string as configured.</param>
    /// <param name="applicationName">
    /// Tag for the session, used unless the connection string names one itself.
    /// </param>
    public static string Normalize(string connectionString, string applicationName = ApplicationName)
        => Rewrite(connectionString, database: null, applicationName);

    /// <summary>
    /// Returns the connection string pointed at <c>master</c>, otherwise identical to
    /// <see cref="Normalize"/>. Used to read a database's availability from metadata without
    /// connecting to the database itself — <c>master</c> stays reachable while another database is
    /// SINGLE_USER, OFFLINE or RESTORING, and reading it cannot occupy the single-user slot.
    /// </summary>
    public static string ToMaster(string connectionString)
        => Rewrite(connectionString, database: "master", ApplicationName);

    /// <summary>
    /// The initial catalog of the connection string, or null if absent or unparsable.
    /// </summary>
    public static string? DatabaseName(string connectionString)
    {
        if (!TryParse(connectionString, out var builder))
            return null;

        return string.IsNullOrWhiteSpace(builder.InitialCatalog) ? null : builder.InitialCatalog;
    }

    private static string Rewrite(string connectionString, string? database, string applicationName)
    {
        if (!TryParse(connectionString, out var builder))
            return connectionString;

        builder.Pooling = false;

        if (database != null)
        {
            builder.InitialCatalog = database;
        }

        // Only tag connections whose application name was not deliberately chosen in the manifest.
        // ShouldSerialize distinguishes an explicitly set value from the driver's default, which the
        // ApplicationName property itself does not.
        if (!builder.ShouldSerialize("Application Name"))
        {
            builder.ApplicationName = applicationName;
        }

        return builder.ConnectionString;
    }

    private static bool TryParse(string connectionString, out SqlConnectionStringBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            builder = new SqlConnectionStringBuilder();
            return false;
        }

        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            builder = new SqlConnectionStringBuilder();
            return false;
        }
    }
}
