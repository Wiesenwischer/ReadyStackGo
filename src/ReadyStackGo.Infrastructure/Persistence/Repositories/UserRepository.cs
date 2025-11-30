namespace ReadyStackGo.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.Identity.Aggregates;
using ReadyStackGo.Domain.Identity.Repositories;
using ReadyStackGo.Domain.Identity.ValueObjects;

/// <summary>
/// SQLite-backed implementation of IUserRepository.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly ReadyStackGoDbContext _context;

    public UserRepository(ReadyStackGoDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public UserId NextIdentity() => UserId.Create();

    public void Add(User user)
    {
        _context.Users.Add(user);
        _context.SaveChanges();
    }

    public User? Get(UserId id)
    {
        return _context.Users
            .Include("_roleAssignments")
            .FirstOrDefault(u => u.Id == id);
    }

    public User? FindByUsername(string username)
    {
        return _context.Users
            .Include("_roleAssignments")
            .FirstOrDefault(u => u.Username == username);
    }

    public User? FindByEmail(EmailAddress email)
    {
        return _context.Users
            .Include("_roleAssignments")
            .FirstOrDefault(u => u.Email == email);
    }

    public IEnumerable<User> GetByTenant(TenantId tenantId)
    {
        return _context.Users
            .Include("_roleAssignments")
            .Where(u => u.TenantId == tenantId)
            .ToList();
    }

    public void Remove(User user)
    {
        _context.Users.Remove(user);
        _context.SaveChanges();
    }
}
