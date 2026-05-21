using System.Text.Json;
using FluentAssertions;
using ReadyStackGo.Application.Integrations.Prtg;
using ReadyStackGo.Application.Snmp;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using Xunit;

namespace ReadyStackGo.UnitTests.Application.Integrations.Prtg;

public class PrtgJsonStatusBuilderTests
{
    private static SnmpSystemInfo System(bool dbHealthy = true) =>
        new("0.66.0", UptimeHundredthsOfSeconds: 12_345_678, EnvironmentCount: 2, SourceCount: 5,
            DbHealthy: dbHealthy, BuildTimestamp: new DateTime(2026, 5, 21, 7, 0, 0, DateTimeKind.Utc));

    private static SnmpProductEntry Product(string name, ProductDeploymentStatus status, int opMode = 0)
        => new(1, 1, $"prod-{name}", name, "1.0", (int)status, status.ToString(),
               opMode, TotalStacks: 3, RunningStacks: 3, FailedStacks: 0,
               LastDeployedAt: null, ErrorMessage: "");

    private static SnmpStackEntry Stack(int idx, StackDeploymentStatus status)
        => new(1, 1, idx, $"stack-{idx}", (int)status, status.ToString(), ServiceCount: 1, Order: idx, ErrorMessage: "");

    private static SnmpServiceEntry Service(int idx, bool running)
        => new(1, 1, 1, idx, $"svc-{idx}", $"svc-{idx}-c", running, HealthStatus: 0, RestartCount: 0,
               LastHealthCheck: null);

    private static SnmpSnapshot MakeSnapshot(
        IReadOnlyList<SnmpProductEntry>? products = null,
        IReadOnlyList<SnmpStackEntry>? stacks = null,
        IReadOnlyList<SnmpServiceEntry>? services = null,
        bool dbHealthy = true) =>
        new(System(dbHealthy),
            new[] { new SnmpEnvironmentEntry(1, "env-1", "default", 0) },
            products ?? Array.Empty<SnmpProductEntry>(),
            stacks ?? Array.Empty<SnmpStackEntry>(),
            services ?? Array.Empty<SnmpServiceEntry>(),
            BuiltAt: DateTime.UtcNow);

    private readonly PrtgJsonStatusBuilder _builder = new();

    [Fact]
    public void Build_EmptySnapshot_AllChannelsZero()
    {
        var snap = MakeSnapshot();
        var resp = _builder.Build(snap);

        resp.Prtg.Result.Should().HaveCountGreaterThan(0);
        foreach (var c in resp.Prtg.Result)
        {
            // System scalars (Environments=1, DB health=1, Uptime>0) are allowed
            // to be non-zero — only the *count* channels of deployments must be 0.
            if (c.Channel.StartsWith("Products") || c.Channel.StartsWith("Stacks") || c.Channel.StartsWith("Services"))
            {
                c.Value.Should().Be("0", $"{c.Channel} should be 0 in an empty snapshot");
            }
        }

        resp.Prtg.Text.Should().Be("No active product deployments");
    }

    [Fact]
    public void Build_RunningAndFailed_ChannelsCountCorrectly()
    {
        var snap = MakeSnapshot(
            products: new[]
            {
                Product("ams.identity", ProductDeploymentStatus.Running),
                Product("ams.project", ProductDeploymentStatus.Running),
                Product("ams.tooling", ProductDeploymentStatus.Failed),
            },
            stacks: new[]
            {
                Stack(1, StackDeploymentStatus.Running),
                Stack(2, StackDeploymentStatus.Running),
                Stack(3, StackDeploymentStatus.Failed),
            },
            services: new[]
            {
                Service(1, running: true),
                Service(2, running: false),
            });

        var resp = _builder.Build(snap);
        var byName = resp.Prtg.Result.ToDictionary(c => c.Channel);

        byName["Products total"].Value.Should().Be("3");
        byName["Products healthy"].Value.Should().Be("2");
        byName["Products failed"].Value.Should().Be("1");
        byName["Stacks total"].Value.Should().Be("3");
        byName["Stacks running"].Value.Should().Be("2");
        byName["Stacks failed"].Value.Should().Be("1");
        byName["Services running"].Value.Should().Be("1");
        byName["Services not running"].Value.Should().Be("1");

        resp.Prtg.Text.Should().Contain("ams.tooling FAILED");
    }

    [Fact]
    public void Build_FailedChannel_HasErrorLimit()
    {
        var snap = MakeSnapshot();
        var resp = _builder.Build(snap);

        var failedProducts = resp.Prtg.Result.Single(c => c.Channel == "Products failed");
        failedProducts.LimitMaxError.Should().Be(0);
        failedProducts.LimitMode.Should().Be(1);

        var failedStacks = resp.Prtg.Result.Single(c => c.Channel == "Stacks failed");
        failedStacks.LimitMaxError.Should().Be(0);
        failedStacks.LimitMode.Should().Be(1);

        var servicesDown = resp.Prtg.Result.Single(c => c.Channel == "Services not running");
        servicesDown.LimitMaxError.Should().Be(0);
        servicesDown.LimitMode.Should().Be(1);
    }

    [Fact]
    public void Build_Maintenance_NotCountedAsFailureAndShownInText()
    {
        var snap = MakeSnapshot(products: new[]
        {
            Product("ams.project", ProductDeploymentStatus.Running, opMode: 1),
            Product("ams.identity", ProductDeploymentStatus.Running, opMode: 0),
        });

        var resp = _builder.Build(snap);
        var byName = resp.Prtg.Result.ToDictionary(c => c.Channel);

        byName["Products in maintenance"].Value.Should().Be("1");
        byName["Products failed"].Value.Should().Be("0", "maintenance must not count as failure");
        resp.Prtg.Text.Should().Contain("ams.project in maintenance");
    }

    [Fact]
    public void Build_DbHealth_UsesPrtgStandardLookup()
    {
        var resp = _builder.Build(MakeSnapshot(dbHealthy: true));

        var db = resp.Prtg.Result.Single(c => c.Channel == "DB health");
        db.Value.Should().Be("1");
        db.ValueLookup.Should().Be("prtg.standardlookups.yesno.stateyesok",
            "use PRTG's built-in lookup so admins don't have to import a custom OVL");
    }

    [Fact]
    public void Build_DbHealthFalse_ValueIsZero()
    {
        var resp = _builder.Build(MakeSnapshot(dbHealthy: false));

        resp.Prtg.Result.Single(c => c.Channel == "DB health").Value.Should().Be("0");
    }

    [Fact]
    public void Build_Uptime_ConvertsHundredthsToSeconds()
    {
        // 12_345_678 / 100 = 123456
        var resp = _builder.Build(MakeSnapshot());

        var uptime = resp.Prtg.Result.Single(c => c.Channel == "Uptime");
        uptime.Value.Should().Be("123456");
        uptime.Unit.Should().Be("TimeSeconds");
    }

    [Fact]
    public void Build_ChannelCount_StaysWellUnderPrtg50Limit()
    {
        var resp = _builder.Build(MakeSnapshot());

        resp.Prtg.Result.Should().HaveCountLessThanOrEqualTo(50);
        // Currently 13 — leaving plenty of room.
        resp.Prtg.Result.Count.Should().BeLessThanOrEqualTo(20);
    }

    [Fact]
    public void Build_TextIsTruncatedWhenManyProductsInTrouble()
    {
        // Generate 50 failed products with long names; text must stay under 250 chars.
        var products = Enumerable.Range(0, 50)
            .Select(i => Product($"product-with-a-rather-long-name-{i}", ProductDeploymentStatus.Failed))
            .ToList();

        var resp = _builder.Build(MakeSnapshot(products: products));

        resp.Prtg.Text!.Length.Should().BeLessThanOrEqualTo(250);
        resp.Prtg.Text!.Should().EndWith("…", "truncation marker should be appended");
    }

    [Fact]
    public void Build_JsonSerialization_UsesPrtgCaseSensitiveAttributes()
    {
        var resp = _builder.Build(MakeSnapshot());
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        var json = JsonSerializer.Serialize(resp, options);

        // PRTG's JSON keys are case-sensitive — verify the exact spelling.
        json.Should().Contain("\"prtg\":");
        json.Should().Contain("\"result\":");
        json.Should().Contain("\"channel\":");
        json.Should().Contain("\"value\":");
        json.Should().Contain("\"ValueLookup\":\"prtg.standardlookups.yesno.stateyesok\"");
    }

    [Fact]
    public void Build_NullSnapshot_Throws()
    {
        Action act = () => _builder.Build(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
