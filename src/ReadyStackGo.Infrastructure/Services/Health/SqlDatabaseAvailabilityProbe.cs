using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>
/// Determines whether a product database may be read, by asking the server about it rather than
/// connecting to it.
///
/// A product taking its database exclusively (an ERP update switching it to SINGLE_USER, a restore)
/// makes the maintenance flag inside that database unreadable — and any connect attempt by RSGO
/// either fails with error 924 or, worse, occupies the one available single-user slot and breaks the
/// product's own update. <c>sys.databases</c> lives in <c>master</c>, which stays reachable
/// throughout and takes no lock on the target database, so RSGO can observe the exclusive window
/// from the outside and wait it out.
/// </summary>
public sealed class SqlDatabaseAvailabilityProbe : ISqlDatabaseAvailabilityProbe
{
    private const string Query = @"
        SELECT state_desc, user_access_desc
        FROM sys.databases
        WHERE name = @database";

    private readonly ILogger<SqlDatabaseAvailabilityProbe> _logger;

    public SqlDatabaseAvailabilityProbe(ILogger<SqlDatabaseAvailabilityProbe> logger)
    {
        _logger = logger;
    }

    public async Task<SqlDatabaseAvailability> ProbeAsync(
        string observerConnectionString,
        CancellationToken cancellationToken = default)
    {
        var database = SqlObserverConnection.DatabaseName(observerConnectionString);
        if (database == null)
        {
            // No initial catalog to reason about — nothing to gate.
            return SqlDatabaseAvailability.Unknown("connection string names no database");
        }

        try
        {
            await using var connection = new SqlConnection(SqlObserverConnection.ToMaster(observerConnectionString));
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(Query, connection);
            command.CommandTimeout = 15;
            command.Parameters.AddWithValue("@database", database);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                // The login cannot see the database in sys.databases (missing VIEW ANY DATABASE, or
                // the name is wrong). Not a maintenance signal — let the caller decide.
                return SqlDatabaseAvailability.Unknown($"database '{database}' not visible in sys.databases");
            }

            var state = reader.IsDBNull(0) ? "UNKNOWN" : reader.GetString(0);
            var userAccess = reader.IsDBNull(1) ? "UNKNOWN" : reader.GetString(1);
            var detail = $"{state}/{userAccess}";

            var isAvailable =
                state.Equals("ONLINE", StringComparison.OrdinalIgnoreCase) &&
                userAccess.Equals("MULTI_USER", StringComparison.OrdinalIgnoreCase);

            return isAvailable
                ? SqlDatabaseAvailability.Available(detail)
                : SqlDatabaseAvailability.Exclusive(detail);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Cannot reach master at all. Reporting Unknown (rather than Exclusive) keeps a
            // deployment from being parked in maintenance because of an unrelated connectivity or
            // permission problem.
            _logger.LogDebug(ex, "Availability probe for database '{Database}' failed", database);
            return SqlDatabaseAvailability.Unknown(ex.Message);
        }
    }
}
