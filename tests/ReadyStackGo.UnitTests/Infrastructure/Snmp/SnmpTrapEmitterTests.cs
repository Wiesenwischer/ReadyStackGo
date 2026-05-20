using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ReadyStackGo.Application.Snmp;
using ReadyStackGo.Domain.Snmp;
using ReadyStackGo.Infrastructure.Snmp;

namespace ReadyStackGo.UnitTests.Infrastructure.Snmp;

public class SnmpTrapEmitterTests : IAsyncLifetime
{
    private UdpClient _receiver = null!;
    private int _receiverPort;
    private FakeSettingsRepository _settingsRepo = null!;
    private SnmpTrapEmitter _emitter = null!;

    public Task InitializeAsync()
    {
        _receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        _receiverPort = ((IPEndPoint)_receiver.Client.LocalEndPoint!).Port;
        _settingsRepo = new FakeSettingsRepository(BuildSettings(enabled: true, community: "public", receivers: $"127.0.0.1:{_receiverPort}"));

        var scopeFactory = new FakeScopeFactory(_settingsRepo);
        _emitter = new SnmpTrapEmitter(scopeFactory, NullLogger<SnmpTrapEmitter>.Instance);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _receiver.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Emit_WhenAgentDisabled_DoesNothing()
    {
        _settingsRepo.Settings = BuildSettings(enabled: false, community: "public", receivers: $"127.0.0.1:{_receiverPort}");

        var trap = new SnmpTrap(SnmpTrapOids.TrapProductDeploymentFailed,
            new[] { new SnmpTrapVariable(SnmpTrapOids.VarProductName, SnmpTrapValueType.OctetString, "ams.project") });

        await _emitter.EmitAsync(trap);
        var arrived = await TryReceiveAsync(TimeSpan.FromMilliseconds(300));
        arrived.Should().BeNull();
    }

    [Fact]
    public async Task Emit_WhenCommunityEmpty_DoesNothing()
    {
        _settingsRepo.Settings = BuildSettings(enabled: true, community: string.Empty, receivers: $"127.0.0.1:{_receiverPort}");

        var trap = new SnmpTrap(SnmpTrapOids.TrapProductDeploymentFailed,
            new[] { new SnmpTrapVariable(SnmpTrapOids.VarProductName, SnmpTrapValueType.OctetString, "ams.project") });

        await _emitter.EmitAsync(trap);
        var arrived = await TryReceiveAsync(TimeSpan.FromMilliseconds(300));
        arrived.Should().BeNull();
    }

    [Fact]
    public async Task Emit_WhenReceiversEmpty_DoesNothing()
    {
        _settingsRepo.Settings = BuildSettings(enabled: true, community: "public", receivers: string.Empty);

        var trap = new SnmpTrap(SnmpTrapOids.TrapProductDeploymentFailed,
            new[] { new SnmpTrapVariable(SnmpTrapOids.VarProductName, SnmpTrapValueType.OctetString, "ams.project") });

        await _emitter.EmitAsync(trap);
        var arrived = await TryReceiveAsync(TimeSpan.FromMilliseconds(300));
        arrived.Should().BeNull();
    }

    [Fact]
    public async Task Emit_ProductDeploymentFailedTrap_ArrivesAtReceiver()
    {
        var trap = new SnmpTrap(SnmpTrapOids.TrapProductDeploymentFailed,
            new[]
            {
                new SnmpTrapVariable(SnmpTrapOids.VarProductName, SnmpTrapValueType.OctetString, "ams.project"),
                new SnmpTrapVariable(SnmpTrapOids.VarMessage, SnmpTrapValueType.OctetString, "stack X failed"),
            });

        await _emitter.EmitAsync(trap);

        var arrived = await TryReceiveAsync(TimeSpan.FromSeconds(2));
        arrived.Should().NotBeNull();

        var messages = MessageFactory.ParseMessages(arrived!, new UserRegistry());
        messages.Should().ContainSingle();
        var trapV2 = messages[0] as TrapV2Message;
        trapV2.Should().NotBeNull();

        trapV2.Enterprise.ToString().Should().Contain("99999.1.6.1");

        var variables = trapV2.Pdu().Variables;
        var productNameVb = variables.FirstOrDefault(v => v.Id.ToString().Contains("99999.1.7.2.0"));
        productNameVb.Should().NotBeNull("the productName varbind should be present");
        productNameVb!.Data.ToString().Should().Be("ams.project");
    }

    private async Task<byte[]?> TryReceiveAsync(TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            var result = await _receiver.ReceiveAsync(cts.Token);
            return result.Buffer;
        }
        catch (OperationCanceledException) { return null; }
    }

    private static SnmpSettings BuildSettings(bool enabled, string community, string receivers)
    {
        var s = SnmpSettings.CreateDefault();
        s.Update(enabled, port: 1161, listenAddress: "0.0.0.0",
            rootOid: "1.3.6.1.4.1.99999.1", community: community, trapReceivers: receivers);
        return s;
    }

    private sealed class FakeSettingsRepository : ISnmpSettingsRepository
    {
        public SnmpSettings Settings { get; set; }
        public FakeSettingsRepository(SnmpSettings settings) => Settings = settings;
        public SnmpSettings GetOrCreate() => Settings;
        public void Update(SnmpSettings settings) => Settings = settings;
        public void SaveChanges() { }
    }

    private sealed class FakeScopeFactory : IServiceScopeFactory
    {
        private readonly ISnmpSettingsRepository _repo;
        public FakeScopeFactory(ISnmpSettingsRepository repo) => _repo = repo;
        public IServiceScope CreateScope() => new FakeScope(_repo);
    }

    private sealed class FakeScope : IServiceScope, IServiceProvider
    {
        private readonly ISnmpSettingsRepository _repo;
        public FakeScope(ISnmpSettingsRepository repo) => _repo = repo;
        public IServiceProvider ServiceProvider => this;
        public object? GetService(Type serviceType) =>
            serviceType == typeof(ISnmpSettingsRepository) ? _repo : null;
        public void Dispose() { }
    }
}
