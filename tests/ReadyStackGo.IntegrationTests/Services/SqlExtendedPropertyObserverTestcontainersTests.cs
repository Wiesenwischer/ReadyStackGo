using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Infrastructure.Observers;
using Testcontainers.MsSql;

namespace ReadyStackGo.IntegrationTests.Services;

/// <summary>
/// Real integration tests for SQL Extended Property Observer using Testcontainers.
/// These tests spin up an actual SQL Server container to verify the observer works correctly.
/// </summary>
public class SqlExtendedPropertyObserverTestcontainersTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer;
    private string _connectionString = null!;

    public SqlExtendedPropertyObserverTestcontainersTests()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        _connectionString = _sqlContainer.GetConnectionString();

        // Create the extended property
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Add extended property for maintenance mode (initially set to "0" = normal)
        await using var cmd = new SqlCommand(
            "EXEC sp_addextendedproperty @name = N'ams.maintenance', @value = '0'",
            connection);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
    }

    [Fact]
    public async Task SqlExtendedPropertyObserver_ReadsPropertyValue_ReturnsNormalMode()
    {
        // Arrange
        var settings = SqlObserverSettings.ForExtendedProperty("ams.maintenance", _connectionString);
        var config = MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.FromSeconds(30),
            maintenanceValue: "1",
            normalValue: "0",
            settings);

        var observer = new SqlExtendedPropertyObserver(config, NullLogger<SqlExtendedPropertyObserver>.Instance);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("0");
        result.IsMaintenanceRequired.Should().BeFalse();
    }

    [Fact]
    public async Task SqlExtendedPropertyObserver_AfterSettingToMaintenance_ReturnsMaintenanceMode()
    {
        // Arrange - Set property to "1" (maintenance)
        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            await using var cmd = new SqlCommand(
                "EXEC sp_updateextendedproperty @name = N'ams.maintenance', @value = '1'",
                connection);
            await cmd.ExecuteNonQueryAsync();
        }

        var settings = SqlObserverSettings.ForExtendedProperty("ams.maintenance", _connectionString);
        var config = MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.FromSeconds(30),
            maintenanceValue: "1",
            normalValue: "0",
            settings);

        var observer = new SqlExtendedPropertyObserver(config, NullLogger<SqlExtendedPropertyObserver>.Instance);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("1");
        result.IsMaintenanceRequired.Should().BeTrue();
    }

    [Fact]
    public async Task SqlExtendedPropertyObserver_MaintenanceToNormal_DetectsChange()
    {
        // Arrange - Start with maintenance mode
        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            await using var cmd = new SqlCommand(
                "EXEC sp_updateextendedproperty @name = N'ams.maintenance', @value = '1'",
                connection);
            await cmd.ExecuteNonQueryAsync();
        }

        var settings = SqlObserverSettings.ForExtendedProperty("ams.maintenance", _connectionString);
        var config = MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.FromSeconds(30),
            maintenanceValue: "1",
            normalValue: "0",
            settings);

        var observer = new SqlExtendedPropertyObserver(config, NullLogger<SqlExtendedPropertyObserver>.Instance);

        // Act - First check (maintenance)
        var maintenanceResult = await observer.CheckAsync();

        // Change to normal
        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            await using var cmd = new SqlCommand(
                "EXEC sp_updateextendedproperty @name = N'ams.maintenance', @value = '0'",
                connection);
            await cmd.ExecuteNonQueryAsync();
        }

        // Second check (normal)
        var normalResult = await observer.CheckAsync();

        // Assert
        maintenanceResult.IsMaintenanceRequired.Should().BeTrue();
        normalResult.IsMaintenanceRequired.Should().BeFalse();
    }

    [Fact]
    public async Task SqlExtendedPropertyObserver_NonExistentProperty_ReturnsNormalValue()
    {
        // Arrange
        // Note: Current implementation treats missing property as normal operation
        // This is the expected behavior - a missing property means "not in maintenance mode"
        var settings = SqlObserverSettings.ForExtendedProperty("non.existent.property", _connectionString);
        var config = MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.FromSeconds(30),
            maintenanceValue: "1",
            normalValue: "0",
            settings);

        var observer = new SqlExtendedPropertyObserver(config, NullLogger<SqlExtendedPropertyObserver>.Instance);

        // Act
        var result = await observer.CheckAsync();

        // Assert - Missing property = normal operation (not maintenance)
        result.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("0");
        result.IsMaintenanceRequired.Should().BeFalse();
    }

    [Fact]
    public async Task SqlExtendedPropertyObserver_InvalidConnectionString_ReturnsFailed()
    {
        // Arrange
        var settings = SqlObserverSettings.ForExtendedProperty(
            "ams.maintenance",
            "Server=nonexistent;Database=test;User Id=sa;Password=wrong;Connect Timeout=1");

        var config = MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.FromSeconds(30),
            maintenanceValue: "1",
            normalValue: "0",
            settings);

        var observer = new SqlExtendedPropertyObserver(config, NullLogger<SqlExtendedPropertyObserver>.Instance);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SqlExtendedPropertyObserver_WithDifferentMaintenanceValues_DetectsCorrectly()
    {
        // Arrange - Use "WARTUNG" instead of "1"
        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            await using var cmd = new SqlCommand(
                "EXEC sp_updateextendedproperty @name = N'ams.maintenance', @value = 'WARTUNG'",
                connection);
            await cmd.ExecuteNonQueryAsync();
        }

        var settings = SqlObserverSettings.ForExtendedProperty("ams.maintenance", _connectionString);
        var config = MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.FromSeconds(30),
            maintenanceValue: "WARTUNG",  // Custom maintenance value
            normalValue: "NORMAL",
            settings);

        var observer = new SqlExtendedPropertyObserver(config, NullLogger<SqlExtendedPropertyObserver>.Instance);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("WARTUNG");
        result.IsMaintenanceRequired.Should().BeTrue();
    }
}
