using MediatR;

namespace ReadyStackGo.Application.Integrations.Prtg;

/// <summary>
/// Builds the PRTG "HTTP Data Advanced" JSON payload from the live SNMP
/// snapshot. Used by an HTTP endpoint a PRTG sensor polls directly — single
/// sensor, no device-template install, no probe restart.
/// </summary>
public sealed record GetPrtgJsonStatusQuery() : IRequest<PrtgJsonStatusResponse>;
