using Lextm.SharpSnmpLib;
using ReadyStackGo.Application.Snmp;

namespace ReadyStackGo.Infrastructure.Snmp;

/// <summary>
/// Materializes a SnmpSnapshot into a sorted OID -> value dictionary that the
/// SNMP agent serves to GET / GETNEXT / WALK requests.
///
/// OID layout under <c>RootOid</c>:
///   .1  rsgoSystem        — scalar values, ending in .0
///   .2  rsgoEnvironmentTable
///   .3  rsgoProductTable
///   .4  rsgoStackTable
///   .5  rsgoServiceTable
/// </summary>
public static class OidTreeBuilder
{
    public const int SystemSubtree = 1;
    public const int EnvironmentTable = 2;
    public const int ProductTable = 3;
    public const int StackTable = 4;
    public const int ServiceTable = 5;

    public static MaterializedOidTree Build(SnmpSnapshot snapshot, string rootOid)
    {
        var root = ParseRoot(rootOid);
        var entries = new Dictionary<ObjectIdentifier, ISnmpData>();

        AddSystem(entries, root, snapshot.System);
        AddEnvironments(entries, root, snapshot.Environments);
        AddProducts(entries, root, snapshot.Products);
        AddStacks(entries, root, snapshot.Stacks);
        AddServices(entries, root, snapshot.Services);

        return new MaterializedOidTree(entries);
    }

    private static uint[] ParseRoot(string rootOid)
    {
        var parts = rootOid.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var result = new uint[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!uint.TryParse(parts[i], out result[i]))
            {
                throw new InvalidOperationException($"Snmp:RootOid contains a non-numeric component: '{parts[i]}'.");
            }
        }
        return result;
    }

    private static void AddSystem(IDictionary<ObjectIdentifier, ISnmpData> data, uint[] root, SnmpSystemInfo system)
    {
        // .1.<column>.0
        data[Oid(root, SystemSubtree, 1, 0)] = new OctetString(system.Version ?? string.Empty);
        data[Oid(root, SystemSubtree, 2, 0)] = new TimeTicks((uint)Math.Min(system.UptimeHundredthsOfSeconds, uint.MaxValue));
        data[Oid(root, SystemSubtree, 3, 0)] = new Integer32(system.EnvironmentCount);
        data[Oid(root, SystemSubtree, 4, 0)] = new Integer32(system.SourceCount);
        data[Oid(root, SystemSubtree, 5, 0)] = new Integer32(system.DbHealthy ? 1 : 0);
        data[Oid(root, SystemSubtree, 6, 0)] = ToDateAndTime(system.BuildTimestamp);
    }

    private static void AddEnvironments(IDictionary<ObjectIdentifier, ISnmpData> data, uint[] root, IReadOnlyList<SnmpEnvironmentEntry> envs)
    {
        // table = .2, entry = .2.1, columns = .2.1.<column>.<envIdx>
        foreach (var env in envs)
        {
            data[Oid(root, EnvironmentTable, 1, 1, env.EnvironmentIndex)] = new Integer32(env.EnvironmentIndex);
            data[Oid(root, EnvironmentTable, 1, 2, env.EnvironmentIndex)] = new OctetString(env.EnvironmentId);
            data[Oid(root, EnvironmentTable, 1, 3, env.EnvironmentIndex)] = new OctetString(env.Name);
            data[Oid(root, EnvironmentTable, 1, 4, env.EnvironmentIndex)] = new Integer32(env.EnvironmentType);
        }
    }

    private static void AddProducts(IDictionary<ObjectIdentifier, ISnmpData> data, uint[] root, IReadOnlyList<SnmpProductEntry> products)
    {
        // table = .3, entry = .3.1, columns = .3.1.<col>.<envIdx>.<prodIdx>
        foreach (var p in products)
        {
            data[Oid(root, ProductTable, 1, 1, p.EnvironmentIndex, p.ProductIndex)] = new Integer32(p.EnvironmentIndex);
            data[Oid(root, ProductTable, 1, 2, p.EnvironmentIndex, p.ProductIndex)] = new Integer32(p.ProductIndex);
            data[Oid(root, ProductTable, 1, 3, p.EnvironmentIndex, p.ProductIndex)] = new OctetString(p.ProductId);
            data[Oid(root, ProductTable, 1, 4, p.EnvironmentIndex, p.ProductIndex)] = new OctetString(p.Name);
            data[Oid(root, ProductTable, 1, 5, p.EnvironmentIndex, p.ProductIndex)] = new OctetString(p.Version);
            data[Oid(root, ProductTable, 1, 6, p.EnvironmentIndex, p.ProductIndex)] = new Integer32(p.Status);
            data[Oid(root, ProductTable, 1, 7, p.EnvironmentIndex, p.ProductIndex)] = new OctetString(p.StatusText);
            data[Oid(root, ProductTable, 1, 8, p.EnvironmentIndex, p.ProductIndex)] = new Integer32(p.OperationMode);
            data[Oid(root, ProductTable, 1, 9, p.EnvironmentIndex, p.ProductIndex)] = new Integer32(p.TotalStacks);
            data[Oid(root, ProductTable, 1, 10, p.EnvironmentIndex, p.ProductIndex)] = new Integer32(p.RunningStacks);
            data[Oid(root, ProductTable, 1, 11, p.EnvironmentIndex, p.ProductIndex)] = new Integer32(p.FailedStacks);
            data[Oid(root, ProductTable, 1, 12, p.EnvironmentIndex, p.ProductIndex)] = ToDateAndTime(p.LastDeployedAt ?? DateTime.MinValue);
            data[Oid(root, ProductTable, 1, 13, p.EnvironmentIndex, p.ProductIndex)] = new OctetString(p.ErrorMessage);
        }
    }

    private static void AddStacks(IDictionary<ObjectIdentifier, ISnmpData> data, uint[] root, IReadOnlyList<SnmpStackEntry> stacks)
    {
        // table = .4, entry = .4.1, columns = .4.1.<col>.<envIdx>.<prodIdx>.<stackIdx>
        foreach (var s in stacks)
        {
            data[Oid(root, StackTable, 1, 1, s.EnvironmentIndex, s.ProductIndex, s.StackIndex)] = new Integer32(s.EnvironmentIndex);
            data[Oid(root, StackTable, 1, 2, s.EnvironmentIndex, s.ProductIndex, s.StackIndex)] = new Integer32(s.ProductIndex);
            data[Oid(root, StackTable, 1, 3, s.EnvironmentIndex, s.ProductIndex, s.StackIndex)] = new Integer32(s.StackIndex);
            data[Oid(root, StackTable, 1, 4, s.EnvironmentIndex, s.ProductIndex, s.StackIndex)] = new OctetString(s.Name);
            data[Oid(root, StackTable, 1, 5, s.EnvironmentIndex, s.ProductIndex, s.StackIndex)] = new Integer32(s.Status);
            data[Oid(root, StackTable, 1, 6, s.EnvironmentIndex, s.ProductIndex, s.StackIndex)] = new OctetString(s.StatusText);
            data[Oid(root, StackTable, 1, 7, s.EnvironmentIndex, s.ProductIndex, s.StackIndex)] = new Integer32(s.ServiceCount);
            data[Oid(root, StackTable, 1, 8, s.EnvironmentIndex, s.ProductIndex, s.StackIndex)] = new Integer32(s.Order);
            data[Oid(root, StackTable, 1, 9, s.EnvironmentIndex, s.ProductIndex, s.StackIndex)] = new OctetString(s.ErrorMessage);
        }
    }

    private static void AddServices(IDictionary<ObjectIdentifier, ISnmpData> data, uint[] root, IReadOnlyList<SnmpServiceEntry> services)
    {
        // table = .5, entry = .5.1, columns = .5.1.<col>.<envIdx>.<prodIdx>.<stackIdx>.<serviceIdx>
        foreach (var s in services)
        {
            data[Oid(root, ServiceTable, 1, 1, s.EnvironmentIndex, s.ProductIndex, s.StackIndex, s.ServiceIndex)] = new Integer32(s.EnvironmentIndex);
            data[Oid(root, ServiceTable, 1, 2, s.EnvironmentIndex, s.ProductIndex, s.StackIndex, s.ServiceIndex)] = new Integer32(s.ProductIndex);
            data[Oid(root, ServiceTable, 1, 3, s.EnvironmentIndex, s.ProductIndex, s.StackIndex, s.ServiceIndex)] = new Integer32(s.StackIndex);
            data[Oid(root, ServiceTable, 1, 4, s.EnvironmentIndex, s.ProductIndex, s.StackIndex, s.ServiceIndex)] = new Integer32(s.ServiceIndex);
            data[Oid(root, ServiceTable, 1, 5, s.EnvironmentIndex, s.ProductIndex, s.StackIndex, s.ServiceIndex)] = new OctetString(s.Name);
            data[Oid(root, ServiceTable, 1, 6, s.EnvironmentIndex, s.ProductIndex, s.StackIndex, s.ServiceIndex)] = new OctetString(s.ContainerName);
            data[Oid(root, ServiceTable, 1, 7, s.EnvironmentIndex, s.ProductIndex, s.StackIndex, s.ServiceIndex)] = new Integer32(s.Running ? 1 : 0);
            data[Oid(root, ServiceTable, 1, 8, s.EnvironmentIndex, s.ProductIndex, s.StackIndex, s.ServiceIndex)] = new Integer32(s.HealthStatus);
            data[Oid(root, ServiceTable, 1, 9, s.EnvironmentIndex, s.ProductIndex, s.StackIndex, s.ServiceIndex)] = new Counter32((uint)Math.Max(0, s.RestartCount));
            data[Oid(root, ServiceTable, 1, 10, s.EnvironmentIndex, s.ProductIndex, s.StackIndex, s.ServiceIndex)] = ToDateAndTime(s.LastHealthCheck ?? DateTime.MinValue);
        }
    }

    private static ObjectIdentifier Oid(uint[] root, params int[] tail)
    {
        var result = new uint[root.Length + tail.Length];
        Array.Copy(root, result, root.Length);
        for (var i = 0; i < tail.Length; i++)
        {
            result[root.Length + i] = (uint)tail[i];
        }
        return new ObjectIdentifier(result);
    }

    private static OctetString ToDateAndTime(DateTime utc)
    {
        // RFC 2579 DateAndTime: 11-octet form (with timezone offset).
        // y[0] y[1] month day hour minute second deci-second '+/-' h_offset m_offset
        if (utc == DateTime.MinValue)
        {
            return new OctetString(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, (byte)'+', 0, 0 });
        }

        var year = (ushort)utc.Year;
        var bytes = new byte[11];
        bytes[0] = (byte)(year >> 8);
        bytes[1] = (byte)(year & 0xFF);
        bytes[2] = (byte)utc.Month;
        bytes[3] = (byte)utc.Day;
        bytes[4] = (byte)utc.Hour;
        bytes[5] = (byte)utc.Minute;
        bytes[6] = (byte)utc.Second;
        bytes[7] = (byte)(utc.Millisecond / 100);
        bytes[8] = (byte)'+';
        bytes[9] = 0;
        bytes[10] = 0;
        return new OctetString(bytes);
    }
}
