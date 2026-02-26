using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Infrastructure.Services.Health;

namespace ReadyStackGo.UnitTests.Infrastructure.Health;

/// <summary>
/// Unit tests for HttpHealthChecker.
/// Tests the full health check flow including JSON parsing of ASP.NET Core HealthReport entries.
/// </summary>
public class HttpHealthCheckerTests
{
    private readonly Mock<ILogger<HttpHealthChecker>> _loggerMock = new();

    private HttpHealthChecker CreateChecker(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new MockHttpMessageHandler(responseContent, statusCode);
        var httpClient = new HttpClient(handler);
        return new HttpHealthChecker(httpClient, _loggerMock.Object);
    }

    private static HttpHealthCheckConfig DefaultConfig => new()
    {
        Path = "/hc",
        Port = 80,
        Timeout = TimeSpan.FromSeconds(5),
        HealthyStatusCodes = new[] { 200 }
    };

    #region Simple String Responses

    [Fact]
    public async Task CheckHealth_SimpleHealthyString_ReturnsHealthy()
    {
        var checker = CreateChecker("Healthy");
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.IsHealthy.Should().BeTrue();
        result.ReportedStatus.Should().Be("Healthy");
        result.Entries.Should().BeNull();
    }

    [Fact]
    public async Task CheckHealth_SimpleUnhealthyString_ReturnsUnhealthy()
    {
        var checker = CreateChecker("Unhealthy");
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.IsHealthy.Should().BeFalse();
        result.ReportedStatus.Should().Be("Unhealthy");
    }

    [Fact]
    public async Task CheckHealth_SimpleDegradedString_ReturnsNotHealthy()
    {
        var checker = CreateChecker("Degraded");
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.IsHealthy.Should().BeFalse();
        result.ReportedStatus.Should().Be("Degraded");
    }

    #endregion

    #region Full HealthReport JSON

    [Fact]
    public async Task CheckHealth_FullHealthReport_ParsesAllEntries()
    {
        var json = """
        {
          "status": "Degraded",
          "entries": {
            "database": {
              "status": "Healthy",
              "description": "SQL Server connection OK",
              "duration": "00:00:00.0123456",
              "data": { "server": "sql01", "database": "ams_project" },
              "tags": ["db", "critical"],
              "exception": null
            },
            "disk": {
              "status": "Degraded",
              "description": "Disk space low on /data",
              "duration": "00:00:00.0001234",
              "data": { "freeSpace": "5GB", "totalSpace": "100GB" },
              "tags": ["infrastructure"],
              "exception": null
            },
            "redis": {
              "status": "Unhealthy",
              "description": "Connection refused",
              "duration": "00:00:05.0012345",
              "data": {},
              "tags": ["cache"],
              "exception": "System.Net.Sockets.SocketException: Connection refused"
            }
          }
        }
        """;

        var checker = CreateChecker(json);
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.IsHealthy.Should().BeFalse();
        result.ReportedStatus.Should().Be("Degraded");
        result.Entries.Should().NotBeNull();
        result.Entries.Should().HaveCount(3);

        // Database entry
        var db = result.Entries!.Single(e => e.Name == "database");
        db.Status.Should().Be("Healthy");
        db.Description.Should().Be("SQL Server connection OK");
        db.DurationMs.Should().BeApproximately(12.3456, 0.01);
        db.Data.Should().ContainKey("server").WhoseValue.Should().Be("sql01");
        db.Data.Should().ContainKey("database").WhoseValue.Should().Be("ams_project");
        db.Tags.Should().Contain("db");
        db.Tags.Should().Contain("critical");
        db.Exception.Should().BeNull();

        // Disk entry
        var disk = result.Entries!.Single(e => e.Name == "disk");
        disk.Status.Should().Be("Degraded");
        disk.Description.Should().Be("Disk space low on /data");
        disk.Tags.Should().ContainSingle("infrastructure");

        // Redis entry (with exception)
        var redis = result.Entries!.Single(e => e.Name == "redis");
        redis.Status.Should().Be("Unhealthy");
        redis.Exception.Should().Contain("SocketException");
        redis.Data.Should().NotBeNull();
        redis.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckHealth_HealthyReport_ParsesEntries()
    {
        var json = """
        {
          "status": "Healthy",
          "entries": {
            "database": {
              "status": "Healthy",
              "description": "OK"
            }
          }
        }
        """;

        var checker = CreateChecker(json);
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.IsHealthy.Should().BeTrue();
        result.Entries.Should().HaveCount(1);
        result.Entries![0].Name.Should().Be("database");
        result.Entries![0].Status.Should().Be("Healthy");
    }

    #endregion

    #region Minimal / Partial JSON

    [Fact]
    public async Task CheckHealth_MinimalEntries_OnlyStatus_ParsesCorrectly()
    {
        var json = """
        {
          "status": "Healthy",
          "entries": {
            "check1": { "status": "Healthy" }
          }
        }
        """;

        var checker = CreateChecker(json);
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.Entries.Should().HaveCount(1);
        var entry = result.Entries![0];
        entry.Name.Should().Be("check1");
        entry.Status.Should().Be("Healthy");
        entry.Description.Should().BeNull();
        entry.DurationMs.Should().BeNull();
        entry.Data.Should().BeNull();
        entry.Tags.Should().BeNull();
        entry.Exception.Should().BeNull();
    }

    [Fact]
    public async Task CheckHealth_NoEntries_ReturnsNullEntries()
    {
        var json = """{ "status": "Healthy" }""";

        var checker = CreateChecker(json);
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.IsHealthy.Should().BeTrue();
        result.Entries.Should().BeNull();
    }

    [Fact]
    public async Task CheckHealth_EmptyEntries_ReturnsEmptyList()
    {
        var json = """{ "status": "Healthy", "entries": {} }""";

        var checker = CreateChecker(json);
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.Entries.Should().NotBeNull();
        result.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckHealth_NullExceptionField_ParsesAsNull()
    {
        var json = """
        {
          "status": "Healthy",
          "entries": {
            "check": { "status": "Healthy", "exception": null }
          }
        }
        """;

        var checker = CreateChecker(json);
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.Entries![0].Exception.Should().BeNull();
    }

    #endregion

    #region Duration Parsing

    [Fact]
    public async Task CheckHealth_TimeSpanDuration_ParsedToMs()
    {
        var json = """
        {
          "status": "Healthy",
          "entries": {
            "check": { "status": "Healthy", "duration": "00:00:01.5000000" }
          }
        }
        """;

        var checker = CreateChecker(json);
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.Entries![0].DurationMs.Should().BeApproximately(1500.0, 0.1);
    }

    [Fact]
    public async Task CheckHealth_SubMillisecondDuration_ParsedCorrectly()
    {
        var json = """
        {
          "status": "Healthy",
          "entries": {
            "check": { "status": "Healthy", "duration": "00:00:00.0001234" }
          }
        }
        """;

        var checker = CreateChecker(json);
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.Entries![0].DurationMs.Should().BeApproximately(0.1234, 0.001);
    }

    [Fact]
    public async Task CheckHealth_NumericDuration_ParsedAsMs()
    {
        var json = """
        {
          "status": "Healthy",
          "entries": {
            "check": { "status": "Healthy", "duration": 42.5 }
          }
        }
        """;

        var checker = CreateChecker(json);
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.Entries![0].DurationMs.Should().Be(42.5);
    }

    [Fact]
    public async Task CheckHealth_InvalidDurationString_ReturnsNull()
    {
        var json = """
        {
          "status": "Healthy",
          "entries": {
            "check": { "status": "Healthy", "duration": "not-a-timespan" }
          }
        }
        """;

        var checker = CreateChecker(json);
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.Entries![0].DurationMs.Should().BeNull();
    }

    #endregion

    #region Alternative Property Names

    [Fact]
    public async Task CheckHealth_PascalCaseProperties_ParsedCorrectly()
    {
        var json = """
        {
          "Status": "Healthy",
          "Entries": {
            "database": {
              "Status": "Healthy",
              "Description": "OK",
              "Duration": "00:00:00.0100000",
              "Data": { "key": "value" },
              "Tags": ["tag1"],
              "Exception": null
            }
          }
        }
        """;

        var checker = CreateChecker(json);
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.IsHealthy.Should().BeTrue();
        result.Entries.Should().HaveCount(1);
        result.Entries![0].Description.Should().Be("OK");
        result.Entries![0].Data.Should().ContainKey("key");
        result.Entries![0].Tags.Should().ContainSingle("tag1");
    }

    [Fact]
    public async Task CheckHealth_ResultsPropertyName_ParsedCorrectly()
    {
        var json = """
        {
          "status": "Healthy",
          "results": {
            "check1": { "status": "Healthy" }
          }
        }
        """;

        var checker = CreateChecker(json);
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.Entries.Should().HaveCount(1);
        result.Entries![0].Name.Should().Be("check1");
    }

    #endregion

    #region Invalid / Edge Cases

    [Fact]
    public async Task CheckHealth_InvalidJson_ReturnsNoEntries()
    {
        var checker = CreateChecker("not valid json at all");
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        // Falls back to HTTP status code check (200 = healthy)
        result.IsHealthy.Should().BeTrue();
        result.Entries.Should().BeNull();
    }

    [Fact]
    public async Task CheckHealth_EmptyResponse_FallsBackToStatusCode()
    {
        var checker = CreateChecker("");
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.IsHealthy.Should().BeTrue();
        result.Entries.Should().BeNull();
    }

    [Fact]
    public async Task CheckHealth_ResponseTimeMsPopulated()
    {
        var checker = CreateChecker("Healthy");
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.ResponseTimeMs.Should().NotBeNull();
        result.ResponseTimeMs!.Value.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task CheckHealth_EntryWithMissingStatusField_DefaultsToUnknown()
    {
        var json = """
        {
          "status": "Healthy",
          "entries": {
            "check": { "description": "No status field" }
          }
        }
        """;

        var checker = CreateChecker(json);
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.Entries![0].Status.Should().Be("Unknown");
    }

    [Fact]
    public async Task CheckHealth_MultipleDataEntries_AllParsed()
    {
        var json = """
        {
          "status": "Healthy",
          "entries": {
            "check": {
              "status": "Healthy",
              "data": {
                "key1": "value1",
                "key2": "value2",
                "key3": "value3"
              }
            }
          }
        }
        """;

        var checker = CreateChecker(json);
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.Entries![0].Data.Should().HaveCount(3);
        result.Entries![0].Data!["key1"].Should().Be("value1");
        result.Entries![0].Data!["key2"].Should().Be("value2");
        result.Entries![0].Data!["key3"].Should().Be("value3");
    }

    [Fact]
    public async Task CheckHealth_NonObjectEntries_ReturnsNullEntries()
    {
        var json = """{ "status": "Healthy", "entries": "not-an-object" }""";

        var checker = CreateChecker(json);
        var result = await checker.CheckHealthAsync("localhost", DefaultConfig);

        result.Entries.Should().BeNull();
    }

    #endregion

    #region MockHttpMessageHandler

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _content;
        private readonly HttpStatusCode _statusCode;

        public MockHttpMessageHandler(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _content = content;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content)
            });
        }
    }

    #endregion
}
