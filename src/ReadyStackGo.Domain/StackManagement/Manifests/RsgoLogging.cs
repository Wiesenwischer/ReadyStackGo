namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Container log rotation for a service (manifest key <c>logging</c>).
/// Overrides the RSGO default log limit for this service only; fields left out keep the default.
/// </summary>
public class RsgoLogging
{
    /// <summary>
    /// Maximum size of one log file before it is rotated (e.g. "10m", "500k", "1g").
    /// Plain numbers are bytes.
    /// </summary>
    public string? MaxSize { get; set; }

    /// <summary>
    /// Maximum number of log files kept per container (at least 1).
    /// </summary>
    public int? MaxFile { get; set; }
}
