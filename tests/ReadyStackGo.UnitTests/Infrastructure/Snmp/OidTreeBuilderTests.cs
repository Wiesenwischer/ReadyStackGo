using FluentAssertions;
using Lextm.SharpSnmpLib;
using ReadyStackGo.Application.Snmp;
using ReadyStackGo.Infrastructure.Snmp;

namespace ReadyStackGo.UnitTests.Infrastructure.Snmp;

public class OidTreeBuilderTests
{
    private const string Root = "1.3.6.1.4.1.65846.1";

    [Fact]
    public void Build_System_PopulatesScalarOids()
    {
        var snapshot = NewSnapshot();
        var tree = OidTreeBuilder.Build(snapshot, Root);

        tree.Get(new ObjectIdentifier($"{Root}.1.1.0"))
            .Should().BeOfType<OctetString>().Which.ToString().Should().Be("v0.64-test");
        tree.Get(new ObjectIdentifier($"{Root}.1.3.0"))
            .Should().BeOfType<Integer32>().Which.ToInt32().Should().Be(2);
    }

    [Fact]
    public void Build_Environment_TableIsIndexedByEnvIndex()
    {
        var snapshot = NewSnapshot();
        var tree = OidTreeBuilder.Build(snapshot, Root);

        var nameOid = new ObjectIdentifier($"{Root}.2.1.3.{snapshot.Environments[0].EnvironmentIndex}");
        tree.Get(nameOid).Should().BeOfType<OctetString>()
            .Which.ToString().Should().Be("Local Docker");
    }

    [Fact]
    public void Build_Product_TableIsIndexedByEnvAndProductIndex()
    {
        var snapshot = NewSnapshot();
        var tree = OidTreeBuilder.Build(snapshot, Root);

        var env = snapshot.Environments[0];
        var prod = snapshot.Products[0];
        var statusOid = new ObjectIdentifier($"{Root}.3.1.6.{env.EnvironmentIndex}.{prod.ProductIndex}");

        tree.Get(statusOid).Should().BeOfType<Integer32>()
            .Which.ToInt32().Should().Be(prod.Status);
    }

    [Fact]
    public void GetNext_RootOid_ReturnsFirstSystemOid()
    {
        var snapshot = NewSnapshot();
        var tree = OidTreeBuilder.Build(snapshot, Root);

        var first = tree.GetNext(new ObjectIdentifier(Root));

        first.Should().NotBeNull();
        // First-in-tree is the first scalar in the System sub-tree.
        first!.Value.Oid.Should().Be(new ObjectIdentifier($"{Root}.1.1.0"));
    }

    [Fact]
    public void GetNext_BeyondLastOid_ReturnsNull()
    {
        var snapshot = NewSnapshot();
        var tree = OidTreeBuilder.Build(snapshot, Root);

        var farFuture = new ObjectIdentifier("1.3.6.1.4.1.65846.1.99.99.99.99");
        tree.GetNext(farFuture).Should().BeNull();
    }

    [Fact]
    public void Build_InvalidRootOid_Throws()
    {
        var act = () => OidTreeBuilder.Build(NewSnapshot(), "1.3.6.foo");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*non-numeric*");
    }

    private static SnmpSnapshot NewSnapshot()
    {
        var envIdx = SnmpIndex.From("env-1");
        var prodIdx = SnmpIndex.From("prod-1");
        var stackIdx = SnmpIndex.From("prod-1", "infrastructure");

        return new SnmpSnapshot(
            System: new SnmpSystemInfo("v0.64-test", 1000, EnvironmentCount: 2, SourceCount: 3, DbHealthy: true, BuildTimestamp: new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc)),
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
}

public class SnmpIndexTests
{
    [Fact]
    public void From_IsDeterministic_ForSameInput()
    {
        SnmpIndex.From("ams.project").Should().Be(SnmpIndex.From("ams.project"));
        SnmpIndex.From("a", "b").Should().Be(SnmpIndex.From("a", "b"));
    }

    [Fact]
    public void From_IsPositive_For31BitRange()
    {
        for (var i = 0; i < 100; i++)
        {
            var hash = SnmpIndex.From($"input-{i}");
            hash.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public void From_TwoComponents_DiffersFromConcatenatedComponents()
    {
        // Without a separator "ab" would collide with ("a", "b"). The separator
        // in SnmpIndex avoids that ambiguity.
        SnmpIndex.From("ab").Should().NotBe(SnmpIndex.From("a", "b"));
    }
}
