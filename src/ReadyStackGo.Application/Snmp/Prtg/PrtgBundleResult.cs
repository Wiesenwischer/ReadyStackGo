namespace ReadyStackGo.Application.Snmp.Prtg;

/// <summary>
/// Result of building a PRTG Device-Template bundle: the ZIP bytes plus
/// the suggested file name (includes the RSGO version so re-downloads
/// don't collide with older bundles on disk).
/// </summary>
public sealed class PrtgBundleResult
{
    public required byte[] ZipBytes { get; init; }
    public required string FileName { get; init; }
    public string ContentType => "application/zip";
}
