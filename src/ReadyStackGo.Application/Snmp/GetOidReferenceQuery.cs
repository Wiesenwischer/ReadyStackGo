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

/// <summary>
/// One concrete table column for a specific row — i.e. a single fully-qualified
/// OID with its symbolic name, type and the value it currently returns. The
/// admin can copy <see cref="Oid"/> straight into PRTG / Zabbix / Nagios.
/// </summary>
public record OidReferenceColumn(
    string Symbol,
    int ColumnNumber,
    string Oid,
    string Type,
    string CurrentValue);

public record OidReferenceEnvironment(
    int EnvironmentIndex,
    string EnvironmentId,
    string Name,
    int EnvironmentType,
    IReadOnlyList<OidReferenceColumn> Columns,
    IReadOnlyList<OidReferenceProduct> Products);

public record OidReferenceProduct(
    int ProductIndex,
    string ProductId,
    string Name,
    string Version,
    int Status,
    string StatusText,
    IReadOnlyList<OidReferenceColumn> Columns,
    IReadOnlyList<OidReferenceStack> Stacks);

public record OidReferenceStack(
    int StackIndex,
    string Name,
    int Status,
    string StatusText,
    IReadOnlyList<OidReferenceColumn> Columns,
    IReadOnlyList<OidReferenceService> Services);

public record OidReferenceService(
    int ServiceIndex,
    string Name,
    string ContainerName,
    bool Running,
    IReadOnlyList<OidReferenceColumn> Columns);
