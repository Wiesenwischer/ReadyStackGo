using ReadyStackGo.Application.Snmp;
using ReadyStackGo.Infrastructure.Snmp;

namespace ReadyStackGo.Api;

/// <summary>
/// Adapter that lets the Application-layer SnmpSettingsChangedHandler reload
/// the Infrastructure-layer SnmpAgent without taking a direct Infrastructure
/// dependency.
/// </summary>
internal sealed class SnmpAgentReloader : ISnmpAgentReloader
{
    private readonly SnmpAgent _agent;
    public SnmpAgentReloader(SnmpAgent agent) => _agent = agent;
    public Task ReloadAsync() => _agent.ReloadAsync();
}
