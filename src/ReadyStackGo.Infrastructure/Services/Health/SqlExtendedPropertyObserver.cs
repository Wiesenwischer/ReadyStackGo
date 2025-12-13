using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>
/// Observer that reads a SQL Server Extended Property to determine maintenance state.
/// Extended Properties are metadata attached to database objects.
/// </summary>
public sealed class SqlExtendedPropertyObserver : BaseMaintenanceObserver
{
    private readonly SqlObserverSettings _settings;

    public SqlExtendedPropertyObserver(
        MaintenanceObserverConfig config,
        ILogger<SqlExtendedPropertyObserver> logger)
        : base(config, logger)
    {
        _settings = config.Settings as SqlObserverSettings
            ?? throw new ArgumentException("Invalid settings type for SQL Extended Property observer");

        if (string.IsNullOrEmpty(_settings.PropertyName))
            throw new ArgumentException("PropertyName is required for SQL Extended Property observer");
    }

    public override ObserverType Type => ObserverType.SqlExtendedProperty;

    protected override async Task<string> GetObservedValueAsync(CancellationToken cancellationToken)
    {
        var connectionString = GetConnectionString();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Query the extended property at database level
        // Extended properties can be at various levels (database, table, column, etc.)
        // We use database-level for application-wide maintenance flags
        const string query = @"
            SELECT CAST(value AS NVARCHAR(MAX))
            FROM sys.extended_properties
            WHERE class = 0 -- Database level
              AND name = @PropertyName";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@PropertyName", _settings.PropertyName);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        if (result == null || result == DBNull.Value)
        {
            Logger.LogDebug("Extended property '{PropertyName}' not found, treating as normal operation",
                _settings.PropertyName);
            return Config.NormalValue ?? string.Empty;
        }

        return result.ToString() ?? string.Empty;
    }

    private string GetConnectionString()
    {
        if (!string.IsNullOrEmpty(_settings.ConnectionString))
        {
            return _settings.ConnectionString;
        }

        // ConnectionName resolution would be handled by manifest variable resolver
        // For now, we expect the resolved connection string to be in ConnectionString
        throw new InvalidOperationException(
            "Connection string not available. ConnectionName should be resolved before creating the observer.");
    }
}
