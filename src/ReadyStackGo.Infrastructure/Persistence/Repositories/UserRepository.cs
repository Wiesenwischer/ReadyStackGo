namespace ReadyStackGo.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;

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

    public void Update(User user)
    {
        _context.Users.Update(user);
        _context.SaveChanges();
    }

    public User? Get(UserId id)
    {
        // Owned types (RoleAssignments) are automatically included
        return _context.Users
            .FirstOrDefault(u => u.Id == id);
    }

    public User? FindByUsername(string username)
    {
        return _context.Users
            .FirstOrDefault(u => u.Username == username);
    }

    public User? FindByEmail(EmailAddress email)
    {
        // Compare by email value since EF Core can't compare owned types directly
        var emailValue = email.Value;
        return _context.Users
            .FirstOrDefault(u => u.Email.Value == emailValue);
    }

    public IEnumerable<User> GetAll()
    {
        return _context.Users.ToList();
    }

    public void Remove(User user)
    {
        _context.Users.Remove(user);
        _context.SaveChanges();
    }
}
