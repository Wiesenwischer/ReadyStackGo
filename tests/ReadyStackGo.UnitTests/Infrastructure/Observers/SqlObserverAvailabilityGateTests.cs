using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Infrastructure.Services.Health;

namespace ReadyStackGo.UnitTests.Infrastructure.Observers;

/// <summary>
/// Unit tests for the availability gate in front of the SQL observers.
///
/// While a product holds its database exclusively (an update switching it to SINGLE_USER, a restore)
/// the maintenance flag inside it cannot be read, and connecting would either fail or occupy the one
/// single-user slot the product's own update needs. The gate answers from server metadata instead.
///
/// The connection string deliberately points at a closed port: any test that ends up reading the
/// database itself fails the check instead of short-circuiting, which is exactly the distinction
/// these tests need to make.
/// </summary>
public class SqlObserverAvailabilityGateTests
{
    private const string UnreachableDb =
        "Server=127.0.0.1,14339;Database=AppDb;User Id=svc;Password=secret;Connect Timeout=1;TrustServerCertificate=true";

    private readonly Mock<ILogger<SqlExtendedPropertyObserver>> _logger = new();

    private static ISqlDatabaseAvailabilityProbe ProbeReturning(SqlDatabaseAvailability availability)
    {
        var probe = new Mock<ISqlDatabaseAvailabilityProbe>();
        probe
            .Setup(p => p.ProbeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(availability);
        return probe.Object;
    }

    private SqlExtendedPropertyObserver CreateObserver(ISqlDatabaseAvailabilityProbe probe)
    {
        var config = MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.FromSeconds(30),
            maintenanceValue: "1",
            normalValue: "0",
            SqlObserverSettings.ForExtendedProperty("app.MaintenanceMode", UnreachableDb));

        return new SqlExtendedPropertyObserver(config, probe, _logger.Object);
    }

    #region Database held exclusively

    [Theory]
    [InlineData("ONLINE/SINGLE_USER")]
    [InlineData("RESTORING/MULTI_USER")]
    [InlineData("OFFLINE/MULTI_USER")]
    [InlineData("ONLINE/RESTRICTED_USER")]
    public async Task CheckAsync_DatabaseNotAvailable_ReportsMaintenanceWithoutReadingIt(string detail)
    {
        var observer = CreateObserver(ProbeReturning(SqlDatabaseAvailability.Exclusive(detail)));

        var result = await observer.CheckAsync();

        result.IsSuccess.Should().BeTrue("an exclusive database is a valid observation, not an error");
        result.IsMaintenanceRequired.Should().BeTrue();
        result.ObservedValue.Should().Contain(detail,
            "the reason belongs in the UI and the maintenance reason text");
    }

    [Fact]
    public async Task CheckAsync_DatabaseNotAvailable_DoesNotTouchTheDatabase()
    {
        var probe = new Mock<ISqlDatabaseAvailabilityProbe>();
        probe
            .Setup(p => p.ProbeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SqlDatabaseAvailability.Exclusive("ONLINE/SINGLE_USER"));

        var observer = CreateObserver(probe.Object);

        var result = await observer.CheckAsync();

        // Reaching the database would have produced a failed result against the closed port.
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        probe.Verify(p => p.ProbeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Database available

    [Fact]
    public async Task CheckAsync_DatabaseAvailable_ReadsTheFlagAsUsual()
    {
        var observer = CreateObserver(ProbeReturning(SqlDatabaseAvailability.Available("ONLINE/MULTI_USER")));

        var result = await observer.CheckAsync();

        // The read itself cannot succeed against a closed port — the point is that the gate did not
        // answer on the observer's behalf, so the automatic return to normal operation stays possible.
        result.IsSuccess.Should().BeFalse();
        result.IsMaintenanceRequired.Should().BeFalse();
    }

    #endregion

    #region Probe unusable

    [Fact]
    public async Task CheckAsync_AvailabilityUnknown_FallsBackToReadingTheFlag()
    {
        var observer = CreateObserver(
            ProbeReturning(SqlDatabaseAvailability.Unknown("VIEW ANY DATABASE denied")));

        var result = await observer.CheckAsync();

        result.IsSuccess.Should().BeFalse();
        result.IsMaintenanceRequired.Should().BeFalse(
            "a permission problem must not park a deployment in maintenance");
    }

    [Fact]
    public async Task CheckAsync_ProbeThrows_IsReportedAsAFailedCheck()
    {
        var probe = new Mock<ISqlDatabaseAvailabilityProbe>();
        probe
            .Setup(p => p.ProbeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("probe exploded"));

        var observer = CreateObserver(probe.Object);

        var result = await observer.CheckAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("probe exploded");
        result.IsMaintenanceRequired.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_Cancelled_PropagatesInsteadOfReportingMaintenance()
    {
        var probe = new Mock<ISqlDatabaseAvailabilityProbe>();
        probe
            .Setup(p => p.ProbeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var observer = CreateObserver(probe.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => observer.CheckAsync());
    }

    #endregion
}
