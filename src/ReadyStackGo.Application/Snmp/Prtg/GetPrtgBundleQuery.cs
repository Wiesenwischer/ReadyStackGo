using MediatR;

namespace ReadyStackGo.Application.Snmp.Prtg;

/// <summary>
/// Builds the PRTG integration bundle (ZIP) the admin can download from the
/// SNMP Monitoring settings page. The handler reads the current Root OID
/// from the live SNMP settings so customers with their own IANA PEN get a
/// bundle that already points at their OID prefix.
/// </summary>
public sealed record GetPrtgBundleQuery(
    byte[] MibBytes,
    string? SourceHost = null,
    string? RsgoVersion = null) : IRequest<PrtgBundleResult>;
