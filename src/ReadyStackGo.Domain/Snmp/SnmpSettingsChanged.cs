using ReadyStackGo.Domain.SharedKernel;

namespace ReadyStackGo.Domain.Snmp;

/// <summary>
/// Raised after SnmpSettings are persisted so the live agent can pick up the
/// change (re-bind the listener if port/listen-address changed, rebuild the
/// UserRegistry, etc.) without a container restart.
/// </summary>
public sealed class SnmpSettingsChanged : DomainEvent
{
}
