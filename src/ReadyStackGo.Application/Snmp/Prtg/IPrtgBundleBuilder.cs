namespace ReadyStackGo.Application.Snmp.Prtg;

/// <summary>
/// Builds the PRTG Device-Template bundle (ZIP) for a given SNMP configuration.
/// Pure, deterministic — the input fully determines the output, so it's easy
/// to unit-test without any external state.
/// </summary>
public interface IPrtgBundleBuilder
{
    PrtgBundleResult Build(PrtgBundleInput input);
}

/// <summary>
/// All inputs the bundle builder needs. The MIB is passed in (rather than
/// loaded by the builder itself) so the source-of-truth MIB lives next to
/// the existing /api/snmp/mib endpoint — no duplicated MIB file.
/// </summary>
public sealed class PrtgBundleInput
{
    /// <summary>SNMP root OID, e.g. <c>1.3.6.1.4.1.99999.1</c>.</summary>
    public required string RootOid { get; init; }

    /// <summary>Raw MIB file content.</summary>
    public required byte[] MibBytes { get; init; }

    /// <summary>RSGO version string; used in the ZIP file name and README.</summary>
    public string? RsgoVersion { get; init; }

    /// <summary>Hostname or URL the request came from; printed into the README for traceability.</summary>
    public string? SourceHost { get; init; }

    /// <summary>UTC timestamp printed into the README.</summary>
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
}
