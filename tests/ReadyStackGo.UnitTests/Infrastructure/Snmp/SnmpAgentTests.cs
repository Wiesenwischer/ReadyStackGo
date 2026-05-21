using System.Net;
using FluentAssertions;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ReadyStackGo.Application.Snmp;
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
        var settings = new SnmpRuntimeSettings(
            Enabled: true,
            Port: _port,
            ListenAddress: "127.0.0.1",
            RootOid: "1.3.6.1.4.1.65846.1",
            Community: "public",
            V3Users: Array.Empty<SnmpRuntimeV3User>(),
            EngineIdHex: "80000186A0040123456789ABCDEF");

        _agent = NewAgent(settings);
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
        var settings = NewSettings(enabled: false, port: GetFreeUdpPort());
        await using var idleAgent = NewAgent(settings);

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
        var requested = new ObjectIdentifier("1.3.6.1.4.1.65846.1.1.1.0");

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
            new ObjectIdentifier("1.3.6.1.4.1.65846.1"),
            collected,
            timeout: 2000,
            WalkMode.WithinSubtree);

        count.Should().Be(0);
        collected.Should().BeEmpty();
    }

    [Fact]
    public void Get_WithWrongCommunity_TimesOut()
    {
        var endpoint = new IPEndPoint(IPAddress.Loopback, _port);

        var act = () => Messenger.Get(
            VersionCode.V2,
            endpoint,
            new OctetString("wrong-community"),
            new List<Variable> { new(new ObjectIdentifier("1.3.6.1.4.1.65846.1.1.1.0")) },
            timeout: 500);

        act.Should().Throw<Lextm.SharpSnmpLib.Messaging.TimeoutException>();
    }

    [Fact]
    public async Task Reload_RebindsListener()
    {
        var newPort = GetFreeUdpPort();
        var scopeFactoryField = _agent.GetType()
            .GetField("_scopeFactory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var scopeFactory = (FakeScopeFactory)scopeFactoryField.GetValue(_agent)!;

        scopeFactory.Provider.Settings = NewSettings(enabled: true, port: newPort);
        await _agent.ReloadAsync();

        _agent.IsRunning.Should().BeTrue();
        _agent.BoundEndpoint!.Port.Should().Be(newPort);
    }

    [Fact]
    public async Task Start_InvalidListenAddress_Throws()
    {
        var settings = new SnmpRuntimeSettings(
            Enabled: true,
            Port: GetFreeUdpPort(),
            ListenAddress: "not-an-ip",
            RootOid: "1.3.6.1.4.1.65846.1",
            Community: string.Empty,
            V3Users: Array.Empty<SnmpRuntimeV3User>(),
            EngineIdHex: "80000186A0040123456789ABCDEF");

        await using var brokenAgent = NewAgent(settings);

        var act = () => brokenAgent.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ListenAddress*");
    }

    private static SnmpRuntimeSettings NewSettings(bool enabled, int port) =>
        new(enabled, port, "127.0.0.1", "1.3.6.1.4.1.65846.1", "public", Array.Empty<SnmpRuntimeV3User>(), "80000186A00401AABBCCDDEEFF");

    private static SnmpAgent NewAgent(SnmpRuntimeSettings settings)
    {
        var provider = new MutableProvider { Settings = settings };
        var scopeFactory = new FakeScopeFactory(provider);
        return new SnmpAgent(scopeFactory, new EmptyOidTree(), NullLogger<SnmpAgent>.Instance);
    }

    private static int GetFreeUdpPort()
    {
        using var probe = new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    private sealed class MutableProvider : ISnmpRuntimeSettingsProvider
    {
        public SnmpRuntimeSettings Settings { get; set; } = new(false, 0, "127.0.0.1", "1.3.6.1.4.1.65846.1", "", Array.Empty<SnmpRuntimeV3User>(), "80000186A00401AABBCCDDEEFF");
        public SnmpRuntimeSettings Load() => Settings;
    }

    private sealed class FakeScopeFactory : IServiceScopeFactory
    {
        public MutableProvider Provider { get; }
        public FakeScopeFactory(MutableProvider provider) => Provider = provider;
        public IServiceScope CreateScope() => new FakeScope(Provider);
    }

    private sealed class FakeScope : IServiceScope, IServiceProvider
    {
        private readonly MutableProvider _provider;
        public FakeScope(MutableProvider provider) => _provider = provider;
        public IServiceProvider ServiceProvider => this;
        public object? GetService(Type serviceType) =>
            serviceType == typeof(ISnmpRuntimeSettingsProvider) ? _provider : null;
        public void Dispose() { }
    }
}
