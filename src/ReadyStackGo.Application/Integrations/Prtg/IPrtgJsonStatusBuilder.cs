using ReadyStackGo.Application.Snmp;

namespace ReadyStackGo.Application.Integrations.Prtg;

/// <summary>
/// Transforms a <see cref="SnmpSnapshot"/> into a PRTG "HTTP Data Advanced"
/// response. Pure, deterministic — input fully determines output, so it's
/// trivially unit-testable.
/// </summary>
public interface IPrtgJsonStatusBuilder
{
    PrtgJsonStatusResponse Build(SnmpSnapshot snapshot);
}
