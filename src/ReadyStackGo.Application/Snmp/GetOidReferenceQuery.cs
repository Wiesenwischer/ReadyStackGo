using MediatR;

namespace ReadyStackGo.Application.Snmp;

/// <summary>
/// Returns the live OID reference for the current RSGO instance. Used by the
/// admin "OID Reference" page so admins can copy the exact OIDs into their
/// monitoring tools without running snmpwalk discovery first.
/// </summary>
public record GetOidReferenceQuery() : IRequest<OidReferenceResult>;

public record OidReferenceResult(
    string RootOid,
    bool SnmpEnabled,
    int Port,
    string ListenAddress,
    IReadOnlyList<OidReferenceScalar> System,
    IReadOnlyList<OidReferenceEnvironment> Environments);

public record OidReferenceScalar(
    string Symbol,
    string Oid,
    string Type,
    string CurrentValue);

public record OidReferenceEnvironment(
    int EnvironmentIndex,
    string EnvironmentId,
    string Name,
    int EnvironmentType,
    string BaseOid,
    IReadOnlyList<OidReferenceProduct> Products);

public record OidReferenceProduct(
    int ProductIndex,
    string ProductId,
    string Name,
    string Version,
    int Status,
    string StatusText,
    string BaseOid,
    IReadOnlyList<OidReferenceStack> Stacks);

public record OidReferenceStack(
    int StackIndex,
    string Name,
    int Status,
    string StatusText,
    string BaseOid,
    IReadOnlyList<OidReferenceService> Services);

public record OidReferenceService(
    int ServiceIndex,
    string Name,
    string ContainerName,
    bool Running,
    string BaseOid);
