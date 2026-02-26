using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.UnitTests.Domain.Health;

/// <summary>
/// Unit tests for HealthCheckEntry value object.
/// </summary>
public class HealthCheckEntryTests
{
    #region Create

    [Fact]
    public void Create_WithAllFields_CreatesEntry()
    {
        var data = new Dictionary<string, string> { { "server", "sql01" } };
        var tags = new List<string> { "db", "critical" };

        var entry = HealthCheckEntry.Create(
            "database",
            HealthStatus.Healthy,
            "SQL Server connection OK",
            12.3,
            data,
            tags,
            null);

        entry.Name.Should().Be("database");
        entry.Status.Should().Be(HealthStatus.Healthy);
        entry.Description.Should().Be("SQL Server connection OK");
        entry.DurationMs.Should().Be(12.3);
        entry.Data.Should().ContainKey("server");
        entry.Data!["server"].Should().Be("sql01");
        entry.Tags.Should().HaveCount(2);
        entry.Tags.Should().Contain("db");
        entry.Tags.Should().Contain("critical");
        entry.Exception.Should().BeNull();
    }

    [Fact]
    public void Create_WithMinimalFields_CreatesEntry()
    {
        var entry = HealthCheckEntry.Create("redis", HealthStatus.Healthy);

        entry.Name.Should().Be("redis");
        entry.Status.Should().Be(HealthStatus.Healthy);
        entry.Description.Should().BeNull();
        entry.DurationMs.Should().BeNull();
        entry.Data.Should().BeNull();
        entry.Tags.Should().BeNull();
        entry.Exception.Should().BeNull();
    }

    [Fact]
    public void Create_WithException_StoresException()
    {
        var entry = HealthCheckEntry.Create(
            "redis",
            HealthStatus.Unhealthy,
            "Connection refused",
            5001.2,
            exception: "System.Net.Sockets.SocketException: Connection refused");

        entry.Status.Should().Be(HealthStatus.Unhealthy);
        entry.Description.Should().Be("Connection refused");
        entry.Exception.Should().Contain("SocketException");
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        var act = () => HealthCheckEntry.Create("", HealthStatus.Healthy);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyData_StoresEmptyDictionary()
    {
        var data = new Dictionary<string, string>();
        var entry = HealthCheckEntry.Create("check", HealthStatus.Healthy, data: data);

        entry.Data.Should().NotBeNull();
        entry.Data.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEmptyTags_StoresEmptyList()
    {
        var tags = new List<string>();
        var entry = HealthCheckEntry.Create("check", HealthStatus.Healthy, tags: tags);

        entry.Tags.Should().NotBeNull();
        entry.Tags.Should().BeEmpty();
    }

    #endregion

    #region Equality

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var entry1 = HealthCheckEntry.Create("database", HealthStatus.Healthy, "OK", 10.5);
        var entry2 = HealthCheckEntry.Create("database", HealthStatus.Healthy, "OK", 10.5);

        entry1.Should().Be(entry2);
        (entry1 == entry2).Should().BeTrue();
        entry1.GetHashCode().Should().Be(entry2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        var entry1 = HealthCheckEntry.Create("database", HealthStatus.Healthy);
        var entry2 = HealthCheckEntry.Create("redis", HealthStatus.Healthy);

        entry1.Should().NotBe(entry2);
        (entry1 != entry2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentStatus_AreNotEqual()
    {
        var entry1 = HealthCheckEntry.Create("database", HealthStatus.Healthy);
        var entry2 = HealthCheckEntry.Create("database", HealthStatus.Unhealthy);

        entry1.Should().NotBe(entry2);
    }

    [Fact]
    public void Equality_DifferentDescription_AreNotEqual()
    {
        var entry1 = HealthCheckEntry.Create("database", HealthStatus.Healthy, "OK");
        var entry2 = HealthCheckEntry.Create("database", HealthStatus.Healthy, "Connection OK");

        entry1.Should().NotBe(entry2);
    }

    [Fact]
    public void Equality_DifferentDuration_AreNotEqual()
    {
        var entry1 = HealthCheckEntry.Create("database", HealthStatus.Healthy, durationMs: 10.0);
        var entry2 = HealthCheckEntry.Create("database", HealthStatus.Healthy, durationMs: 20.0);

        entry1.Should().NotBe(entry2);
    }

    [Fact]
    public void Equality_DifferentException_AreNotEqual()
    {
        var entry1 = HealthCheckEntry.Create("database", HealthStatus.Unhealthy, exception: "Error A");
        var entry2 = HealthCheckEntry.Create("database", HealthStatus.Unhealthy, exception: "Error B");

        entry1.Should().NotBe(entry2);
    }

    #endregion

    #region All HealthStatus Values

    [Theory]
    [InlineData("Healthy")]
    [InlineData("Degraded")]
    [InlineData("Unhealthy")]
    [InlineData("Unknown")]
    public void Create_WithEachStatus_StoresCorrectStatus(string statusName)
    {
        var status = HealthStatus.FromName(statusName);
        var entry = HealthCheckEntry.Create("check", status);

        entry.Status.Should().Be(status);
    }

    #endregion
}
