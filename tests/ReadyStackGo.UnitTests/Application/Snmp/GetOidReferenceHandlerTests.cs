using FluentAssertions;
using Microsoft.Extensions.Options;
using ReadyStackGo.Application.Snmp;

namespace ReadyStackGo.UnitTests.Application.Snmp;

public class GetOidReferenceHandlerTests
{
    private const string Root = "1.3.6.1.4.1.65846.1";

    [Fact]
    public async Task Handle_PopulatesSystemScalarsWithConcreteOids()
    {
        var handler = NewHandler(NewSnapshot());

        var result = await handler.Handle(new GetOidReferenceQuery(), CancellationToken.None);

        result.System.Should().HaveCountGreaterThan(0);
        result.System.Select(s => s.Oid).Should().AllSatisfy(oid =>
            oid.Should().StartWith($"{Root}.1.").And.EndWith(".0"));
    }

    [Fact]
    public async Task Handle_ProductColumns_AreFullyQualifiedConcreteOids()
    {
        // The whole point of this refactor: no <column> placeholder, every column
        // is a complete OID an admin can paste into PRTG/Zabbix/Nagios.

        var snapshot = NewSnapshot();
        var handler = NewHandler(snapshot);
        var product = snapshot.Products[0];

        var result = await handler.Handle(new GetOidReferenceQuery(), CancellationToken.None);

        var productInResult = result.Environments[0].Products[0];
        productInResult.Columns.Should().HaveCount(13, "rsgoProductEntry has 13 columns per MIB");
        productInResult.Columns.Select(c => c.Oid).Should().AllSatisfy(oid =>
            oid.Should().NotContain("<column>"));

        var statusColumn = productInResult.Columns.Single(c => c.Symbol == "rsgoProductStatus");
        statusColumn.ColumnNumber.Should().Be(6);
        statusColumn.Oid.Should().Be($"{Root}.3.1.6.{product.EnvironmentIndex}.{product.ProductIndex}");
        statusColumn.CurrentValue.Should().Contain(product.StatusText);
    }

    [Fact]
    public async Task Handle_StackColumns_IncludeEnvAndProductIndexInOid()
    {
        var snapshot = NewSnapshot();
        var handler = NewHandler(snapshot);
        var stack = snapshot.Stacks[0];

        var result = await handler.Handle(new GetOidReferenceQuery(), CancellationToken.None);

        var stackInResult = result.Environments[0].Products[0].Stacks[0];
        stackInResult.Columns.Should().HaveCount(9, "rsgoStackEntry has 9 columns per MIB");

        var nameColumn = stackInResult.Columns.Single(c => c.Symbol == "rsgoStackName");
        nameColumn.Oid.Should().Be(
            $"{Root}.4.1.4.{stack.EnvironmentIndex}.{stack.ProductIndex}.{stack.StackIndex}");
        nameColumn.CurrentValue.Should().Be(stack.Name);
    }

    [Fact]
    public async Task Handle_ServiceColumns_FullyQualifyAllFourIndices()
    {
        var snapshot = NewSnapshotWithService();
        var handler = NewHandler(snapshot);
        var service = snapshot.Services[0];

        var result = await handler.Handle(new GetOidReferenceQuery(), CancellationToken.None);

        var serviceInResult = result.Environments[0].Products[0].Stacks[0].Columns;
        // sanity — stack still has columns
        serviceInResult.Should().NotBeEmpty();

        var serviceNode = result.Environments[0].Products[0].Stacks[0]
            .Services.Should().ContainSingle().Subject;
        serviceNode.Columns.Should().HaveCount(10, "rsgoServiceEntry has 10 columns per MIB");

        var running = serviceNode.Columns.Single(c => c.Symbol == "rsgoServiceRunning");
        running.ColumnNumber.Should().Be(7);
        running.Oid.Should().Be(
            $"{Root}.5.1.7.{service.EnvironmentIndex}.{service.ProductIndex}.{service.StackIndex}.{service.ServiceIndex}");
        running.CurrentValue.Should().Be("1 (yes)");
    }

    [Fact]
    public async Task Handle_OidsRespectCustomRootOid()
    {
        // Customers with their own IANA PEN configure Snmp:RootOid — the OID
        // reference must follow, otherwise the page lies about what their SNMP
        // agent serves.
        const string customRoot = "1.3.6.1.4.1.12345.42";
        var snapshot = NewSnapshot();
        var handler = NewHandler(snapshot, customRoot);

        var result = await handler.Handle(new GetOidReferenceQuery(), CancellationToken.None);

        result.RootOid.Should().Be(customRoot);
        result.Environments[0].Columns.Should().AllSatisfy(c => c.Oid.Should().StartWith(customRoot));
        result.Environments[0].Products[0].Columns.Should().AllSatisfy(c => c.Oid.Should().StartWith(customRoot));
    }

    [Fact]
    public async Task Handle_EmptySnapshot_ReturnsEmptyEnvironmentsListNotNull()
    {
        var handler = NewHandler(EmptySnapshot());

        var result = await handler.Handle(new GetOidReferenceQuery(), CancellationToken.None);

        result.Environments.Should().BeEmpty();
        result.System.Should().NotBeEmpty("system scalars exist regardless of environment count");
    }

    private static GetOidReferenceHandler NewHandler(SnmpSnapshot snapshot, string rootOid = Root)
    {
        var provider = new StubSnapshotProvider(snapshot);
        var options = Options.Create(new SnmpAgentOptions { RootOid = rootOid, Enabled = true });
        return new GetOidReferenceHandler(provider, options);
    }

    private static SnmpSnapshot NewSnapshot()
    {
        var envIdx = SnmpIndex.From("env-1");
        var prodIdx = SnmpIndex.From("prod-1");
        var stackIdx = SnmpIndex.From("prod-1", "infrastructure");

        return new SnmpSnapshot(
            System: new SnmpSystemInfo("v0.66-test", 12345, EnvironmentCount: 1, SourceCount: 2, DbHealthy: true,
                BuildTimestamp: new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc)),
            Environments: new List<SnmpEnvironmentEntry>
            {
                new(envIdx, "env-1", "Local Docker", 0),
            },
            Products: new List<SnmpProductEntry>
            {
                new(envIdx, prodIdx, "stacks:demo:1.0.0", "Demo", "1.0.0",
                    Status: 1, StatusText: "Running",
                    OperationMode: 0, TotalStacks: 1, RunningStacks: 1, FailedStacks: 0,
                    LastDeployedAt: new DateTime(2026, 5, 20, 11, 0, 0, DateTimeKind.Utc),
                    ErrorMessage: string.Empty),
            },
            Stacks: new List<SnmpStackEntry>
            {
                new(envIdx, prodIdx, stackIdx, "Infrastructure", 2, "Running", 3, 0, string.Empty),
            },
            Services: Array.Empty<SnmpServiceEntry>(),
            BuiltAt: DateTime.UtcNow);
    }

    private static SnmpSnapshot NewSnapshotWithService()
    {
        var envIdx = SnmpIndex.From("env-1");
        var prodIdx = SnmpIndex.From("prod-1");
        var stackIdx = SnmpIndex.From("prod-1", "infrastructure");
        var svcIdx = SnmpIndex.From("prod-1", "infrastructure", "eventstore");

        var baseline = NewSnapshot();
        return baseline with
        {
            Services = new List<SnmpServiceEntry>
            {
                new(envIdx, prodIdx, stackIdx, svcIdx, "eventstore", "rsgo-eventstore",
                    Running: true, HealthStatus: 1, RestartCount: 0,
                    LastHealthCheck: new DateTime(2026, 5, 21, 9, 0, 0, DateTimeKind.Utc)),
            },
        };
    }

    private static SnmpSnapshot EmptySnapshot() => new(
        System: new SnmpSystemInfo("v0.66-test", 0, 0, 0, DbHealthy: true, BuildTimestamp: DateTime.UtcNow),
        Environments: Array.Empty<SnmpEnvironmentEntry>(),
        Products: Array.Empty<SnmpProductEntry>(),
        Stacks: Array.Empty<SnmpStackEntry>(),
        Services: Array.Empty<SnmpServiceEntry>(),
        BuiltAt: DateTime.UtcNow);

    private sealed class StubSnapshotProvider(SnmpSnapshot snapshot) : ISnmpSnapshotProvider
    {
        public SnmpSnapshot GetCurrentSnapshot() => snapshot;
    }
}
