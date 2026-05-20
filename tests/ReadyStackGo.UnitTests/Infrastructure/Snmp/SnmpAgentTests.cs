using System.Net;
using FluentAssertions;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReadyStackGo.Infrastructure.Snmp;

namespace ReadyStackGo.UnitTests.Infrastructure.Snmp;

/// <summary>
/// Validates the SNMP agent's UDP listener end-to-end by sending real SNMP v2c
/// GET / GETNEXT messages with SharpSnmpLib and asserting on the decoded
/// response. The agent's OID tree is the EmptyOidTree stub so every request
/// must come back as noSuchObject / endOfMibView.
/// </summary>
public class SnmpAgentTests : IAsyncLifetime
{
    private SnmpAgent _agent = null!;
    private int _port;

    public async Task InitializeAsync()
    {
        _port = GetFreeUdpPort();
        var options = Options.Create(new SnmpAgentOptions
        {
            Enabled = true,
            Port = _port,
            ListenAddress = "127.0.0.1",
        });

        _agent = new SnmpAgent(options, new EmptyOidTree(), NullLogger<SnmpAgent>.Instance);
        await _agent.StartAsync(CancellationToken.None);
        _agent.IsRunning.Should().BeTrue();
    }

    public async Task DisposeAsync()
    {
        await _agent.StopAsync(CancellationToken.None);
        _agent.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Disabled_DoesNotBindSocket()
    {
        var disabledOptions = Options.Create(new SnmpAgentOptions { Enabled = false });
        await using var idleAgent = new SnmpAgent(
            disabledOptions, new EmptyOidTree(), NullLogger<SnmpAgent>.Instance);

        await idleAgent.StartAsync(CancellationToken.None);

        idleAgent.IsRunning.Should().BeFalse();
        idleAgent.BoundEndpoint.Should().BeNull();
    }

    [Fact]
    public void Start_BindsConfiguredEndpoint()
    {
        _agent.BoundEndpoint.Should().NotBeNull();
        _agent.BoundEndpoint!.Address.Should().Be(IPAddress.Loopback);
        _agent.BoundEndpoint.Port.Should().Be(_port);
    }

    [Fact]
    public void Get_UnknownOid_ReturnsNoSuchObject()
    {
        var endpoint = new IPEndPoint(IPAddress.Loopback, _port);
        var requested = new ObjectIdentifier("1.3.6.1.4.1.99999.1.1.1.0");

        var result = Messenger.Get(
            VersionCode.V2,
            endpoint,
            new OctetString("public"),
            new List<Variable> { new(requested) },
            timeout: 2000);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(requested);
        result[0].Data.Should().BeOfType<NoSuchObject>();
    }

    [Fact]
    public void Walk_OnEmptyTree_FindsNothing()
    {
        var endpoint = new IPEndPoint(IPAddress.Loopback, _port);
        var collected = new List<Variable>();

        var count = Messenger.Walk(
            VersionCode.V2,
            endpoint,
            new OctetString("public"),
            new ObjectIdentifier("1.3.6.1.4.1.99999.1"),
            collected,
            timeout: 2000,
            WalkMode.WithinSubtree);

        // EmptyOidTree never has a "next" OID, so the walk stops immediately with
        // endOfMibView — no variables get collected.
        count.Should().Be(0);
        collected.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_WhenAlreadyRunning_IsNoOp()
    {
        // First StartAsync happened in InitializeAsync. Second one must not throw.
        await _agent.StartAsync(CancellationToken.None);

        _agent.IsRunning.Should().BeTrue();
        _agent.BoundEndpoint!.Port.Should().Be(_port);
    }

    [Fact]
    public async Task Start_InvalidListenAddress_Throws()
    {
        var options = Options.Create(new SnmpAgentOptions
        {
            Enabled = true,
            Port = GetFreeUdpPort(),
            ListenAddress = "not-an-ip",
        });

        await using var brokenAgent = new SnmpAgent(
            options, new EmptyOidTree(), NullLogger<SnmpAgent>.Instance);

        var act = () => brokenAgent.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ListenAddress*");
    }

    private static int GetFreeUdpPort()
    {
        using var probe = new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }
}
