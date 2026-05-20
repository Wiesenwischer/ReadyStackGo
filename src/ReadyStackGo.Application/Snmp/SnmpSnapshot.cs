namespace ReadyStackGo.Application.Snmp;

/// <summary>
/// In-memory snapshot of the data RSGO publishes via SNMP. The agent and the
/// OID Reference UI both consume this so that what gets exposed via UDP and
/// what the admin sees in the browser stay in sync. The provider rebuilds the
/// snapshot at most once every 30 s.
/// </summary>
public record SnmpSnapshot(
    SnmpSystemInfo System,
    IReadOnlyList<SnmpEnvironmentEntry> Environments,
    IReadOnlyList<SnmpProductEntry> Products,
    IReadOnlyList<SnmpStackEntry> Stacks,
    IReadOnlyList<SnmpServiceEntry> Services,
    DateTime BuiltAt);

public record SnmpSystemInfo(
    string Version,
    long UptimeHundredthsOfSeconds,
    int EnvironmentCount,
    int SourceCount,
    bool DbHealthy,
    DateTime BuildTimestamp);

public record SnmpEnvironmentEntry(
    int EnvironmentIndex,
    string EnvironmentId,
    string Name,
    int EnvironmentType);

public record SnmpProductEntry(
    int EnvironmentIndex,
    int ProductIndex,
    string ProductId,
    string Name,
    string Version,
    int Status,
    string StatusText,
    int OperationMode,
    int TotalStacks,
    int RunningStacks,
    int FailedStacks,
    DateTime? LastDeployedAt,
    string ErrorMessage);

public record SnmpStackEntry(
    int EnvironmentIndex,
    int ProductIndex,
    int StackIndex,
    string Name,
    int Status,
    string StatusText,
    int ServiceCount,
    int Order,
    string ErrorMessage);

public record SnmpServiceEntry(
    int EnvironmentIndex,
    int ProductIndex,
    int StackIndex,
    int ServiceIndex,
    string Name,
    string ContainerName,
    bool Running,
    int HealthStatus,
    int RestartCount,
    DateTime? LastHealthCheck);

public interface ISnmpSnapshotProvider
{
    SnmpSnapshot GetCurrentSnapshot();
}

/// <summary>
/// Maps domain IDs to stable 31-bit positive Int32 indices via GetHashCode.
/// Same input always yields the same index, regardless of snapshot order or
/// insertion sequence, so monitoring tools can store OIDs long-term.
/// </summary>
public static class SnmpIndex
{
    public static int From(string id)
    {
        unchecked
        {
            return (int)((uint)id.GetHashCode() & 0x7FFFFFFF);
        }
    }

    public static int From(string id, string sub)
        => From(id + "/" + sub);

    public static int From(string id, string sub, string subSub)
        => From(id + "/" + sub + "/" + subSub);
}
