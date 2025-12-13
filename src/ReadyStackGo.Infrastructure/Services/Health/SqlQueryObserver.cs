using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>
/// Observer that executes a SQL query to determine maintenance state.
/// The query must return a single scalar value.
/// </summary>
public sealed class SqlQueryObserver : BaseMaintenanceObserver
{
    private readonly SqlObserverSettings _settings;

    public SqlQueryObserver(
        MaintenanceObserverConfig config,
        ILogger<SqlQueryObserver> logger)
        : base(config, logger)
    {
        _settings = config.Settings as SqlObserverSettings
            ?? throw new ArgumentException("Invalid settings type for SQL Query observer");

        if (string.IsNullOrEmpty(_settings.Query))
            throw new ArgumentException("Query is required for SQL Query observer");
    }

    public override ObserverType Type => ObserverType.SqlQuery;

    protected override async Task<string> GetObservedValueAsync(CancellationToken cancellationToken)
    {
        var connectionString = GetConnectionString();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(_settings.Query, connection);
        command.CommandTimeout = 30; // 30 second timeout for the query

        var result = await command.ExecuteScalarAsync(cancellationToken);

        if (result == null || result == DBNull.Value)
        {
            Logger.LogDebug("Query returned null, treating as normal operation");
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

        throw new InvalidOperationException(
            "Connection string not available. ConnectionName should be resolved before creating the observer.");
    }
}
