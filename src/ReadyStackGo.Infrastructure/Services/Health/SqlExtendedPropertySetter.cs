using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>
/// Writes a SQL Server database-level extended property — the write-side mirror of
/// <see cref="SqlExtendedPropertyObserver"/>. Idempotent: it upserts the desired value, so
/// repeating the same transition is a no-op effect and cannot drive a feedback loop with the
/// observer (which only reads).
/// </summary>
public sealed class SqlExtendedPropertySetter : IMaintenanceSetter
{
    private readonly MaintenanceSetterConfig _config;
    private readonly SqlSetterSettings _settings;
    private readonly ILogger<SqlExtendedPropertySetter> _logger;

    public SqlExtendedPropertySetter(
        MaintenanceSetterConfig config,
        ILogger<SqlExtendedPropertySetter> logger)
    {
        _config = config;
        _logger = logger;
        _settings = config.Settings as SqlSetterSettings
            ?? throw new ArgumentException("Invalid settings type for SQL Extended Property setter");
    }

    public SetterType Type => SetterType.SqlExtendedProperty;

    public async Task<SetterResult> SetAsync(MaintenanceState state, CancellationToken cancellationToken = default)
    {
        var desiredValue = _config.ValueForState(state);

        try
        {
            await using var connection = new SqlConnection(_settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            // Idempotent upsert of the database-level extended property.
            const string sql = @"
                IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND name = @name)
                    EXEC sys.sp_updateextendedproperty @name = @name, @value = @value;
                ELSE
                    EXEC sys.sp_addextendedproperty @name = @name, @value = @value;";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@name", _settings.PropertyName);
            command.Parameters.AddWithValue("@value", desiredValue);

            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation(
                "Maintenance setter wrote extended property '{PropertyName}' = '{Value}' (state {State})",
                _settings.PropertyName, desiredValue, state);

            return SetterResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Maintenance setter failed to write extended property '{PropertyName}' (state {State})",
                _settings.PropertyName, state);
            return SetterResult.Failed(ex.Message);
        }
    }
}
