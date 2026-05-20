using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.Snmp;

namespace ReadyStackGo.Infrastructure.DataAccess.Repositories;

public class SnmpSettingsRepository : ISnmpSettingsRepository
{
    private readonly ReadyStackGoDbContext _context;

    public SnmpSettingsRepository(ReadyStackGoDbContext context)
    {
        _context = context;
    }

    public SnmpSettings GetOrCreate()
    {
        var existing = _context.SnmpSettings.FirstOrDefault(s => s.Id == SnmpSettings.SingletonId);
        if (existing is not null) return existing;

        var fresh = SnmpSettings.CreateDefault();
        _context.SnmpSettings.Add(fresh);
        _context.SaveChanges();
        return fresh;
    }

    public void Update(SnmpSettings settings)
    {
        _context.SnmpSettings.Update(settings);
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }
}

public class SnmpV3UserRepository : ISnmpV3UserRepository
{
    private readonly ReadyStackGoDbContext _context;

    public SnmpV3UserRepository(ReadyStackGoDbContext context)
    {
        _context = context;
    }

    public IReadOnlyList<SnmpV3User> GetAll()
        => _context.SnmpV3Users.AsNoTracking().OrderBy(u => u.Name).ToList();

    public SnmpV3User? GetByName(string name)
        => _context.SnmpV3Users.FirstOrDefault(u => u.Name == name);

    public SnmpV3User? GetById(Guid id)
        => _context.SnmpV3Users.FirstOrDefault(u => u.Id == id);

    public void Add(SnmpV3User user) => _context.SnmpV3Users.Add(user);
    public void Update(SnmpV3User user) => _context.SnmpV3Users.Update(user);
    public void Remove(SnmpV3User user) => _context.SnmpV3Users.Remove(user);
    public void SaveChanges() => _context.SaveChanges();
}
