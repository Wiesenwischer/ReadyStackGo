namespace ReadyStackGo.Domain.Snmp;

public interface ISnmpSettingsRepository
{
    /// <summary>
    /// Gets the singleton settings row, creating the default if it does not
    /// exist yet (first-time setup).
    /// </summary>
    SnmpSettings GetOrCreate();

    void Update(SnmpSettings settings);
    void SaveChanges();
}

public interface ISnmpV3UserRepository
{
    IReadOnlyList<SnmpV3User> GetAll();
    SnmpV3User? GetByName(string name);
    SnmpV3User? GetById(Guid id);
    void Add(SnmpV3User user);
    void Update(SnmpV3User user);
    void Remove(SnmpV3User user);
    void SaveChanges();
}
